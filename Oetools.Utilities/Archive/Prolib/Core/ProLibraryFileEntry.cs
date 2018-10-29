#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProLibraryFile.cs) is part of WinPL.
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
using System.IO;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Archive.Prolib.Core {
    
    internal class ProLibraryFileEntry {
        
        private const byte NumberOfNullsAfterFileEntryx32 = 8;
        private const byte NumberOfNullsAfterFileEntryx64 = 24;
        
        private const uint FakeFileSizex32 = 0x13;
        private const uint FakeFileSizex64 = 0x27;
        
        /// <summary>
        /// The file path outside of the .pl (if it exists).
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Length of <see cref="RelativePath"/>.
        /// </summary>
        public byte RelativePathSize { get; private set; }
        
        /// <summary>
        /// Path inside the cab, including path separator characters and the file name.
        /// </summary>
        public string RelativePath {
            get => _relativePath;
            set {
                _relativePath = value?.ToCleanRelativePathUnix();
                if (!string.IsNullOrEmpty(_relativePath)) {
                    if (_relativePath.Length > byte.MaxValue) {
                        throw new ProLibraryException($"The file exceeds the maximum path length of {byte.MaxValue} with {_relativePath.Length} : {_relativePath}.");
                    }
                    RelativePathSize = (byte) _relativePath.Length;
                }
            }
        }

        /// <summary>
        /// The file entry CRC.
        /// </summary>
        private ushort Crc { get; set; }

        /// <summary>
        /// Offset in the .pl at which to read the file data.
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        /// The file type.
        /// </summary>
        public ProLibraryFileType Type { get; set; }
        
        /// <summary>
        /// The file size.
        /// </summary>
        public uint Size { get; set; }
        
        /// <summary>
        /// The time at which the file was packed into the .pl.
        /// </summary>
        public DateTime? PackTime { get; set; }
        
        /// <summary>
        /// The time stamp of this file (last write time).
        /// </summary>
        public DateTime? LastWriteTime { get; set; }
        
        public ProLibrary Parent { get; }
        
        /// <summary>
        /// Each file entry ends with a set number of null bytes (they seem useless), this gets the number of null bytes to add at the end of the file entry.
        /// </summary>
        private byte NumberOfNullsAfterFileEntry => Parent.Is64Bits ? NumberOfNullsAfterFileEntryx64 : NumberOfNullsAfterFileEntryx32;

        /// <summary>
        /// Minus the initial FF.
        /// </summary>
        public int FileEntryLength => 1 + RelativePathSize + 2 + (Parent.Is64Bits ? sizeof(long) : sizeof(uint)) + 1 + 4 + 4 + 4 + NumberOfNullsAfterFileEntry;

        private string _relativePath;

        public ProLibraryFileEntry(ProLibrary parent) {
            Parent = parent;
            Type = ProLibraryFileType.FakeFile;
            Size = parent.Is64Bits ? FakeFileSizex64 : FakeFileSizex32;
        }

        public void ReadFileEntry(BinaryReader reader) {
            var initialOffset = reader.BaseStream.Position;
            RelativePathSize = reader.ReadByte();
            if (RelativePathSize > 0) {
                RelativePath = Parent.CodePage.GetString(reader.ReadBytes(RelativePathSize));
            }
            Crc = reader.ReadUInt16Be();
            Offset = Parent.Is64Bits ? reader.ReadUInt64Be() : reader.ReadUInt32Be();
            var type = reader.ReadByte();
            if (!Enum.IsDefined(typeof(ProLibraryFileType), type)) {
                // let's not crash since prolib actually can read this
                Type = ProLibraryFileType.Other;
            } else {
                Type = (ProLibraryFileType) type;
            }
            Size = reader.ReadUInt32Be();
            PackTime = reader.ReadUInt32Be().GetDatetimeFromUint();
            LastWriteTime = reader.ReadUInt32Be().GetDatetimeFromUint();
            reader.BaseStream.Position += NumberOfNullsAfterFileEntry;
            if (reader.BaseStream.Position - initialOffset != FileEntryLength) {
                throw new ProLibraryException($"Bad file entry, we expected a file entry length of {FileEntryLength} and got {reader.BaseStream.Position - initialOffset}.");
            }
            
            // check CRC
            var computedCrc = GetFileCrc();
            if (Crc != 0 && computedCrc != Crc) {
                throw new ProLibraryException($"Bad file CRC, expected {Crc} but found {computedCrc} for file {RelativePath}, the library might be corrupted.");
            }
        }

        public void WriteFileEntry(BinaryWriter writer) {
            writer.Write(RelativePathSize);
            if (!string.IsNullOrEmpty(RelativePath)) {
                writer.Write(Parent.CodePage.GetBytes(RelativePath));
            }
            writer.WriteUInt16Be(GetFileCrc());
            WriteFileEntryAfterCrc(writer);
        }
        
        private void WriteFileEntryAfterCrc(BinaryWriter writer) {
            if (Parent.Is64Bits) {
                writer.WriteUInt64Be(Offset);
            } else {
                writer.WriteUInt32Be((uint) Offset);
            }
            writer.Write((byte) Type);
            writer.WriteUInt32Be(Size);
            writer.WriteUInt32Be(PackTime?.GetUintFromDateTime() ?? 0);
            writer.WriteUInt32Be(LastWriteTime?.GetUintFromDateTime() ?? 0);
            writer.Write(new byte[NumberOfNullsAfterFileEntry]);
        }
        
        private ushort GetFileCrc() {
            using (var memStream = new MemoryStream()) {
                using (var writer = new BinaryWriter(memStream)) {
                    if (!string.IsNullOrEmpty(RelativePath)) {
                        writer.Write(Parent.CodePage.GetBytes(RelativePath));
                    }
                    WriteFileEntryAfterCrc(writer);
                    return UoeHash.ComputeCrc(0, memStream.ToArray());
                }
            }
        }
    }
}