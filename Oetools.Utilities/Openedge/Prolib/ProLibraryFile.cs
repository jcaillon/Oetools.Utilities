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

namespace Oetools.Utilities.Openedge.Prolib {
    public class ProLibraryFile {
        
        /// <summary>
        /// Absolute file path outside of the .pl (if it exists)
        /// </summary>
        public string AbsolutePath { get; set; }
        
        /// <summary>
        /// Path inside the cab, including path separator characters and the file name
        /// </summary>
        public string RelativePath {
            get => _relativePath;
            set => _relativePath = value?.NormalizeRelativePath();
        }
        
        public DateTime LastWriteTime { get; set; }
        
        public DateTime ArchivedTime { get; set; }
        
        public long Offset { get; set; }
        
        public uint Size { get; set; }

        public byte RelativePathSize { get; set; }
        
        public ushort Crc { get; set; }

        private string _relativePath;
    }
}