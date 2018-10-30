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
    
    /// <summary>
    /// An openedge library (aka prolib aka .pl file).
    /// </summary>
    internal class ProLibrary : IDisposable {

        private const string DefaultCodePageName = "undefined";
        private const byte MaxCodePageNameLength = 24;
        private const byte HeaderExtraBytesLength = 4;
        private const byte ProlibSignatureFirstByte = 0xD7;
        private const byte ReadFileEntry = 0xFF;
        private const byte SkipUntilNextFileEntry = 0xFE;
        private const short DataBufferSize = 1024 * 8;
        
        public ProLibrary(string filePath, CancellationToken? cancelToken) {
            FilePath = filePath;
            _cancelToken = cancelToken;
            OpenLib();
        }

        public void Dispose() {
            _reader?.Dispose();
        }

        private BinaryReader _reader;
        
        private readonly CancellationToken? _cancelToken;
        private string _encodingName;
        private string _filePathToWrite;
        private byte[] _headerExtraBytes;

        public event EventHandler<ProLibrarySaveEventArgs> OnProgress;

        /// <summary>
        /// The file path of this pro library.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Temporary path to write to
        /// </summary>
        private string FilePathToWrite => _filePathToWrite ?? (_filePathToWrite = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", $"~{Path.GetRandomFileName()}"));

        /// <summary>
        /// The pro library version.
        /// </summary>
        public ProLibraryVersion Version { get; set; } = ProLibraryVersion.V11Standard;

        /// <summary>
        /// The code page to use for the encoding the relative path of each file. Defaults to <see cref="DefaultCodePageName"/>.
        /// </summary>
        public string CodePageName {
            get => _encodingName ?? DefaultCodePageName;
            set {
                if (!string.IsNullOrEmpty(value) && UoeUtilities.GetEncodingFromOpenedgeCodePage(value, out var codePage)) {
                    _encodingName = value;
                    CodePage = codePage;
                }
            }
        }
        
        public Encoding CodePage { get; private set; } = Encoding.Default;
        
        /// <summary>
        /// Crc value for this library header.
        /// </summary>
        private ushort HeaderCrc { get; set; }
        
        /// <summary>
        /// The number of file entries. Can be different than the actual number of files( if there are empty files.
        /// Should not be used to determine the number of files.
        /// </summary>
        private ushort NbOfEntries { get; set; }
        
        /// <summary>
        /// The offset in this file stream at which to find the first file entry info.
        /// </summary>
        private long FirstFileEntryOffset { get; set; }

        private byte[] HeaderExtraBytes {
            get => _headerExtraBytes ?? new byte[HeaderExtraBytesLength];
            set => _headerExtraBytes = value;
        }

        /// <summary>
        /// The list of files in this lib.
        /// </summary>
        public List<ProLibraryFileEntry> Files { get; } = new List<ProLibraryFileEntry>();        
        
        /// <summary>
        /// The total header length.
        /// </summary>
        private int HeaderLength => 2 + MaxCodePageNameLength + 2 + 2 + (Is64Bits ? sizeof(long) : sizeof(uint)) + HeaderExtraBytesLength;

        
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
        public bool Exists => File.Exists(FilePath);

        /// <summary>
        /// Is this prolib a 64 bits version.
        /// </summary>
        public bool Is64Bits => Version >= ProLibraryVersion.V11Standard;
        
        /// <summary>
        /// Add a new external file to this prolib.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="relativePathInPl"></param>
        /// <exception cref="ProLibraryException"></exception>
        public void AddExternalFile(string sourcePath, string relativePathInPl) {
            if (Files.Count + 1 > ushort.MaxValue) {
                throw new ProLibraryException($"The prolib would exceed the maximum number of files in a single file: {ushort.MaxValue}.");
            }
            
            // remove existing files with the same name
            DeleteFile(relativePathInPl);

            var fileInfo = new FileInfo(sourcePath);
            var fileInfoLength = fileInfo.Length;
            if (fileInfo.Length > uint.MaxValue) {
                throw new ProLibraryException($"The file exceeds the maximum size of {uint.MaxValue} with a length of {fileInfoLength} bytes.");
            }

            Files.Add(new ProLibraryFileEntry(this) {
                RelativePath = relativePathInPl,
                FilePath = sourcePath,
                Size = (uint) fileInfoLength,
                LastWriteTime = fileInfo.LastWriteTime,
                PackTime = DateTime.Now,
                Type = relativePathInPl.EndsWith(UoeConstants.ExtR, StringComparison.OrdinalIgnoreCase) ? ProLibraryFileType.Rcode : ProLibraryFileType.Other
            });
        }
        
        /// <summary>
        /// Extracts a file to an external path.
        /// </summary>
        /// <param name="relativePathInPl"></param>
        /// <param name="extractionPath"></param>
        /// <returns>true if the file was actually extracted, false if it does not exist</returns>
        public bool ExtractToFile(string relativePathInPl, string extractionPath) {
            var fileToExtract = Files.FirstOrDefault(file => file.RelativePath.Equals(relativePathInPl, StringComparison.OrdinalIgnoreCase));
            if (fileToExtract == null) {
                return false;
            }
                       
            try {
                long totalNumberOfBytes = fileToExtract.Size;
                long totalNumberOfBytesDone = 0;
                _reader.BaseStream.Position = fileToExtract.Offset;
            
                using (Stream targetStream = File.OpenWrite(extractionPath)) {
                    var dataBlockBuffer = new byte[DataBufferSize];
                    long bytesLeftToRead;
                    int nbBytesRead;
                
                    while ((bytesLeftToRead = totalNumberOfBytes - totalNumberOfBytesDone) > 0 && 
                        (nbBytesRead = _reader.Read(dataBlockBuffer, 0, (int) Math.Min(bytesLeftToRead, dataBlockBuffer.Length))) > 0) {
                    
                        totalNumberOfBytesDone += nbBytesRead;
                        targetStream.Write(dataBlockBuffer, 0, nbBytesRead);
                        OnProgress?.Invoke(this, ProLibrarySaveEventArgs.New(relativePathInPl, totalNumberOfBytesDone, totalNumberOfBytes));
                        _cancelToken?.ThrowIfCancellationRequested();
                    }
                }
            } catch(OperationCanceledException) {
                if (File.Exists(extractionPath)) {
                    File.Delete(extractionPath);
                }
                throw;
            }

            if (fileToExtract.LastWriteTime != null) {
                File.SetLastWriteTime(extractionPath, (DateTime) fileToExtract.LastWriteTime);
            }
            return true;
        }

        /// <summary>
        /// Delete a file within this prolib.
        /// </summary>
        /// <param name="relativePathInPl"></param>
        /// <returns></returns>
        public bool DeleteFile(string relativePathInPl) {
            return Files.RemoveAll(f => f.RelativePath.Equals(relativePathInPl, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        /// <summary>
        /// Move (i.e. change the relative path) a file within this prolib.
        /// </summary>
        /// <param name="relativePathInPl"></param>
        /// <param name="newRelativePathInPl"></param>
        /// <returns></returns>
        public bool MoveFile(string relativePathInPl, string newRelativePathInPl) {
            var fileToMove = Files.FirstOrDefault(file => file.RelativePath.Equals(relativePathInPl, StringComparison.OrdinalIgnoreCase));
            if (fileToMove == null) {
                return false;
            }
            fileToMove.RelativePath = newRelativePathInPl;
            return true;
        }
        
        /// <summary>
        /// Save this instance of <see cref="ProLibrary"/> to <see cref="FilePath"/>.
        /// </summary>
        public void Save() {
            var cabDirectory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(cabDirectory) && !Directory.Exists(cabDirectory)) {
                Directory.CreateDirectory(cabDirectory);
            }
            try {
                using (var writer = new BinaryWriter(File.OpenWrite(FilePathToWrite))) {
                    if (Files.Count > 0) {
                        writer.Write(new byte[HeaderLength]);
                        WriteData(writer);
                        FirstFileEntryOffset = writer.BaseStream.Position;
                        // add the last dummy file
                        Files.Add(new ProLibraryFileEntry(this));
                        WriteFileEntries(writer);
                    } else {
                        FirstFileEntryOffset = 0;
                        OnProgress?.Invoke(this, ProLibrarySaveEventArgs.New(string.Empty, 1, 1));
                    }
                    NbOfEntries = (ushort) Files.Count;
                    WriteProlibStructure(writer);
                    if (Files.Count > 1) {
                        // remove dummy file
                        Files.RemoveAt(Files.Count - 1);
                    }
                }
                _reader?.Dispose();
                _cancelToken?.ThrowIfCancellationRequested();
                File.Delete(FilePath);
                File.Move(FilePathToWrite, FilePath);
                _reader = new BinaryReader(File.OpenRead(FilePath));
            } finally {
                // get rid of the temp file
                if (File.Exists(FilePathToWrite)) {
                    File.Delete(FilePathToWrite);
                }
            }
        }

        private void OpenLib() {
            Files.Clear();
            if (Exists) {
                _reader = new BinaryReader(File.OpenRead(FilePath));
                ReadProlibStructure(_reader);
            }
        }
        
        /// <summary>
        /// Read data from <see cref="FilePath"/> to fill this <see cref="ProLibrary"/>.
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
            HeaderExtraBytes = reader.ReadBytes(HeaderExtraBytesLength);
            
            // check CRC
            var computedHeaderCrc = GetHeaderCrc();
            if (HeaderCrc != 0 && computedHeaderCrc != HeaderCrc) {
                throw new ProLibraryException($"Bad header CRC, expected {HeaderCrc} but found {computedHeaderCrc}, the library might be corrupted.");
            }

            // read file entries
            ReadFileEntries(reader);
            if (NbOfEntries != Files.Count) {
                throw new ProLibraryException($"Unexpected number of entries found, expected {NbOfEntries} but found {Files.Count}.");
            }

            // remove all "non" files
            Files.RemoveAll(f => f.Type == ProLibraryFileType.FakeFile || f.RelativePathSize == 0);
            
            // reset the extra bytes to have a clean file. For some reasons, prolib put non null bytes in there sometimes.
            _headerExtraBytes = null;
        }

        private void ReadFileEntries(BinaryReader reader) {
            if (FirstFileEntryOffset == 0) {
                return;
            }
            
            // start reading file entries
            if (FirstFileEntryOffset > FileSize) {
                throw new ProLibraryException($"Bad first entry offset, the offset is {FirstFileEntryOffset} but the total size of this prolib is {FileSize}.");
            }
            
            reader.BaseStream.Position = FirstFileEntryOffset;

            do {
                _cancelToken?.ThrowIfCancellationRequested();
                
                var fileEntry = new ProLibraryFileEntry(this);
                var fileStatus = reader.ReadByte();
                switch (fileStatus) {
                    case ReadFileEntry:
                        Files.Add(fileEntry);
                        fileEntry.ReadFileEntry(reader);
                        break;
                    case SkipUntilNextFileEntry:
                        // skip bytes (usually null bytes) until the next file entry
                        var foundNextFileEntry = false;
                        do {
                            var data = reader.ReadBytes(550);
                            if (data.Length == 0) {
                                // done reading
                                return;
                            }

                            int i;
                            for (i = 0; i < data.Length; i++) {
                                if (data[i] == ReadFileEntry) {
                                    foundNextFileEntry = true;
                                    break;
                                }
                            }
                            reader.BaseStream.Position = reader.BaseStream.Position - data.Length + i;
                        } while (!foundNextFileEntry);
                        break;
                    case 0:
                        // done reading
                        return;
                    default:
                        throw new ProLibraryException($"Unexpected byte found at position {reader.BaseStream.Position}.");
                }
            } while (true);
        }

        private void WriteProlibStructure(BinaryWriter writer) {
            writer.BaseStream.Position = 0;
            writer.Write(ProlibSignatureFirstByte);
            writer.Write((byte) Version);
            var codePageData = new byte[MaxCodePageNameLength];
            Encoding.ASCII.GetBytes(CodePageName).CopyTo(codePageData, 0);
            writer.Write(codePageData);
            writer.WriteUInt16Be(GetHeaderCrc());
            WriteHeaderAfterCrc(writer);
        }

        private void WriteData(BinaryWriter writer) {
            
            long totalNumberOfBytes = TotalSizeFromFiles;
            long totalNumberOfBytesDone = 0;
            
            foreach (var file in Files) {
                _cancelToken?.ThrowIfCancellationRequested();
                var fileOffset = writer.BaseStream.Position;
                
                if (!string.IsNullOrEmpty(file.FilePath)) {
                    if (!File.Exists(file.FilePath)) {
                        throw new ProLibraryException($"Missing source file : {file.FilePath}.");
                    }
                    using (Stream sourceStream = File.OpenRead(file.FilePath)) {
                        if (file.Size != sourceStream.Length) {
                            throw new ProLibraryException($"The size of the source file has changed since it was added, previously {file.Size} bytes and now {sourceStream.Length} bytes.");
                        }
                        var dataBlockBuffer = new byte[DataBufferSize];
                        int nbBytesRead;
                        while ((nbBytesRead = sourceStream.Read(dataBlockBuffer, 0, dataBlockBuffer.Length)) > 0) {
                            totalNumberOfBytesDone += nbBytesRead;
                            writer.Write(dataBlockBuffer, 0, nbBytesRead);
                            OnProgress?.Invoke(this, ProLibrarySaveEventArgs.New(file.RelativePath, totalNumberOfBytesDone, totalNumberOfBytes));
                            _cancelToken?.ThrowIfCancellationRequested();
                        }
                    }
                } else {                   
                    var dataBlockBuffer = new byte[DataBufferSize];
                    long bytesLeftToRead;
                    int nbBytesRead;

                    _reader.BaseStream.Position = file.Offset;
                    long numberOfBytes = file.Size;
                    long numberOfBytesDone = 0;
                
                    while ((bytesLeftToRead = numberOfBytes - numberOfBytesDone) > 0 && 
                        (nbBytesRead = _reader.Read(dataBlockBuffer, 0, (int) Math.Min(bytesLeftToRead, dataBlockBuffer.Length))) > 0) {

                        numberOfBytesDone += nbBytesRead;
                        totalNumberOfBytesDone += nbBytesRead;
                        writer.Write(dataBlockBuffer, 0, nbBytesRead);
                        OnProgress?.Invoke(this, ProLibrarySaveEventArgs.New(file.RelativePath, totalNumberOfBytesDone, totalNumberOfBytes));
                        _cancelToken?.ThrowIfCancellationRequested();
                    }
                }

                file.Offset = fileOffset;
            }
        }

        private void WriteFileEntries(BinaryWriter writer) {
            foreach (var file in Files) {
                writer.Write(ReadFileEntry);
                file.WriteFileEntry(writer);
            }
            writer.Write(SkipUntilNextFileEntry);
        }
        
        private void WriteHeaderAfterCrc(BinaryWriter writer) {
            writer.WriteUInt16Be(NbOfEntries);
            if (Is64Bits) {
                writer.WriteUInt64Be(FirstFileEntryOffset);
            } else {
                writer.WriteUInt32Be((uint) FirstFileEntryOffset);
            }
            writer.Write(HeaderExtraBytes);
        }
        
        private ushort GetHeaderCrc() {
            using (var memStream = new MemoryStream()) {
                using (var writer = new BinaryWriter(memStream)) {
                    WriteHeaderAfterCrc(writer);
                    return UoeHash.ComputeCrc(0, memStream.ToArray());
                }
            }
        }
        
    }
}