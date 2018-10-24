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
using System.Text;
using System.Threading;

namespace Oetools.Utilities.Openedge.Prolib {
    
    public class ProLibrary : IDisposable {

        private const string DefaultEncodingName = "undefined";
        private const long HeaderCrcPosition = 26;
        
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

        public string Path { get; set; }

        public Encoding CodePage { get; set; } = Encoding.Default;

        public string CodePageName {
            get => _encodingName ?? DefaultEncodingName;
            set {
                try {
                    _encodingName = value;
                    CodePage = Encoding.GetEncoding(value);
                } catch (Exception) {
                    _encodingName = DefaultEncodingName;
                    CodePage = Encoding.Default;
                }
            }
        }

        public ProLibraryFormat Format { get; set; }
        
        public List<ProLibraryFile> Files { get; } = new List<ProLibraryFile>();
        
        public long TotalSizeFromFiles {
            get {
                long total = 0;
                foreach (var file in Files) {
                    total += file.Size;
                }
                return total;
            }
        }        
        
        public long FileSize { get; set; }
        
        public uint HeaderCrc { get; set; }

        /// <summary>
        /// The position in the lib at which to read the offset were we can find the first file entry.
        /// </summary>
        private long HeaderFirstFileOffsetPosition {
            get {
                switch (Format) {
                    case ProLibraryFormat.v9Standard:
                        return 30;
                    default:
                        return 34;
                }
            }
        }

        private ushort NbFilesFixOffet {
            get {
                switch (Format) {
                    case ProLibraryFormat.v9Standard:
                        return 31;
                    default:
                        return 43;
                }
            }
        }
        
        /// <summary>
        /// Returns true if this prolib exists.
        /// </summary>
        public bool Exists => File.Exists(Path);
        
        private void OpenLib() {
            Files.Clear();
            if (Exists) {
                _reader = new BinaryReader(File.OpenRead(Path));
                ReadCabinetInfo(_reader);
            }
        }
        
        /// <summary>
        /// Read data from <see cref="Path"/> to fill this <see cref="ProLibrary"/>.
        /// </summary>
        /// <param name="reader"></param>
        private void ReadCabinetInfo(BinaryReader reader) {
            ReadCabinetHeader(reader);
            //ReadFileAndFolderHeaders(reader);
        }
        
        private void ReadCabinetHeader(BinaryReader reader) {
            FileSize = reader.BaseStream.Length;

            // format
            var format = reader.ReadUInt16BE();

            if (!Enum.IsDefined(typeof(ProLibraryFormat), format)) {
                throw new ProLibraryException($"Unknown library format {format}.");
            }

            Format = (ProLibraryFormat) format;

            // code page
            CodePageName = reader.ReadNullTerminatedString(Encoding.ASCII);

            // Header crc
            reader.BaseStream.Position = HeaderCrcPosition;
            HeaderCrc = reader.ReadUInt16BE();

            var nbFiles = reader.ReadUInt16BE();

            // Table of content offset
            reader.BaseStream.Position = HeaderFirstFileOffsetPosition;
            var tocOffset = reader.ReadUInt32BE();


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