#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProLibrary.cs) is part of WinPL.
// 
// WinPL is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// WinPL is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with WinPL. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Archive.Prolib.Core {
    
    public class ProLibrary : IDisposable {

        private const string DefaultCodePageName = "undefined";
        private const byte MaxCodePageNameLength = 24;
        private const byte HeaderExtraNullBytes = 4;
        private const byte ProlibSignatureFirstByte = 0xD7;
        private const byte ReadFileEntry = 0xFF;
        private const byte SkipFileEntry = 0xFE;
        
        public ProLibrary(string path, CancellationToken? cancelToken) {
            _lookupTable = UoeEncryptor.GetConstantLookupTable();
            Path = path;
            _cancelToken = cancelToken;
            OpenLib();
        }

        public void Dispose() {
            _reader?.Dispose();
        }

        private BinaryReader _reader;
        
        private readonly CancellationToken? _cancelToken;
        private string _encodingName;
        private readonly ushort[] _lookupTable;

        /// <summary>
        /// The absolute path of this pro library.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The pro library version.
        /// </summary>
        public ProLibraryVersion Version { get; set; }

        /// <summary>
        /// The code page to use for the encoding the relative path of each file. Defaults to <see cref="DefaultCodePageName"/>.
        /// </summary>
        public string CodePageName {
            get => _encodingName ?? DefaultCodePageName;
            set {
                _encodingName = value;
                UoeUtilities.GetEncodingFromOpenedgeCodePage(value, out var codePage);
                CodePage = codePage;
            }
        }
        
        public Encoding CodePage { get; set; } = Encoding.Default;
        
        /// <summary>
        /// Crc value for this library header.
        /// </summary>
        public uint HeaderCrc { get; set; }
        
        /// <summary>
        /// The number of file entries. Can be different than the actual number of files( if there are empty files.
        /// Should not be used to determine the number of files.
        /// </summary>
        public ushort NbOfEntries { get; set; }
        
        /// <summary>
        /// The offset in this file stream at which to find the first file entry info.
        /// </summary>
        public long FirstFileEntryOffset { get; set; }

        /// <summary>
        /// The list of files in this lib.
        /// </summary>
        public List<ProLibraryFile> Files { get; } = new List<ProLibraryFile>();
        
        /// <summary>
        /// Total files size.
        /// </summary>
        public long TotalSizeFromFiles {
            get {
                long total = 0;
                foreach (var file in Files) {
                    total += file.Size;
                }
                return total;
            }
        }
        
        /// <summary>
        /// This prolib file size.
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// Returns true if this prolib exists.
        /// </summary>
        public bool Exists => File.Exists(Path);

        public bool Is64Bits => Version >= ProLibraryVersion.V11Standard;
        
        private void OpenLib() {
            Files.Clear();
            if (Exists) {
                _reader = new BinaryReader(File.OpenRead(Path));
                ReadProlibStructure(_reader);
            }
        }
        
        /// <summary>
        /// Read data from <see cref="Path"/> to fill this <see cref="ProLibrary"/>.
        /// </summary>
        /// <param name="reader"></param>
        private void ReadProlibStructure(BinaryReader reader) {
            FileSize = reader.BaseStream.Length;
            var version = reader.ReadByte();
            if (version != ProlibSignatureFirstByte) {
                throw new ProLibraryException($"Wrong first byte detected, {version} instead of {ProlibSignatureFirstByte}, is this really a pro library?");
            }
            version = reader.ReadByte();
            if (!Enum.IsDefined(typeof(ProLibraryVersion), version)) {
                throw new ProLibraryException($"Unknown library version {version}.");
            }
            
            Version = (ProLibraryVersion) version;
            
            var codePageNameData = reader.ReadBytes(MaxCodePageNameLength).ToList();
            var idx = codePageNameData.FindIndex(b => b == 0);
            CodePageName = Encoding.ASCII.GetString((idx > 0 ? codePageNameData.Take(idx) : codePageNameData).ToArray());
            
            HeaderCrc = reader.ReadUInt16Be();

            NbOfEntries = reader.ReadUInt16Be();

            FirstFileEntryOffset = Is64Bits ? reader.ReadUInt64Be() : reader.ReadUInt32Be();

            
            
            // start reading file entries
            if (FirstFileEntryOffset > FileSize) {
                throw new ProLibraryException($"Bad first entry offset, the offset is {FirstFileEntryOffset} but the total size of this prolib is {FileSize}.");
            }
            
            reader.BaseStream.Position = FirstFileEntryOffset;

            do {
                var fileEntry = new ProLibraryFile(this);
                var fileStatus = reader.ReadByte();
                switch (fileStatus) {
                    case ReadFileEntry:
                        fileEntry.RelativePathSize = reader.ReadByte();
                        
                        if (fileEntry.RelativePathSize == 0) {
                            // skip file
                            if (reader.BaseStream.Position + fileEntry.FileEntryLength >= FileSize) {
                                throw new ProLibraryException($"Unexpected end of stream, file size is {FileSize} and we needed to position to {reader.BaseStream.Position + fileEntry.FileEntryLength}.");
                            }
                            reader.BaseStream.Position += fileEntry.FileEntryLength;
                            break;
                        }

                        fileEntry.ReadFileEntry(reader);
                        
                        break;
                    case SkipFileEntry:
                        if (1 + reader.BaseStream.Position + fileEntry.FileEntryLength >= FileSize) {
                            // we are done reading
                            return;
                        }
                        reader.BaseStream.Position += 1 + fileEntry.FileEntryLength;
                        break;
                    default:
                        throw new ProLibraryException($"Unexpected byte found at position {reader.BaseStream.Position}.");
                }
            } while (true);
        }
        
        private int ComputeCrc(int initialValue, ref byte[] data, int _param2, int _param3) {
            int num1 = checked(_param2 + _param3 - 1);
            while (_param2 <= num1) {
                int index2 = (initialValue ^ data[_param2]) & byte.MaxValue;
                initialValue = initialValue / 256 ^ _lookupTable[index2];
                _param2++;
            }
            return initialValue;
        }
        
    }
}