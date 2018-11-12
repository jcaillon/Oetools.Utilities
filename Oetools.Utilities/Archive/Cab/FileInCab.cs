#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (CabFileArchived.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;

namespace Oetools.Utilities.Archive.Cab {
    
    internal class FileInCab : IFileInArchive {
        
        public string ArchivePath { get; }
        public string RelativePathInArchive { get; }
        public bool Processed { get; set; }
        public ulong SizeInBytes { get; }
        public DateTime LastWriteTime { get; }

        internal FileInCab(string archivePath, string relativePathInArchive, ulong sizeInBytes, DateTime lastWriteTime) {
            ArchivePath = archivePath;
            RelativePathInArchive = relativePathInArchive;
            SizeInBytes = sizeInBytes;
            LastWriteTime = lastWriteTime;
        }
    }
}