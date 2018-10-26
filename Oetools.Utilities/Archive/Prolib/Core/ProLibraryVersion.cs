#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProLibraryType.cs) is part of WinPL.
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

namespace Oetools.Utilities.Archive.Prolib.Core {
    
    /// <summary>
    /// The prolib version.
    /// </summary>
    public enum ProLibraryVersion : byte {
        
        /// <summary>
        /// Used for standard lib in openedge version lower than version 10.
        /// </summary>
        V7Standard = 0x07,
        
        /// <summary>
        /// Used for memory mapped lib in openedge version lower than version 10.
        /// </summary>
        V8MemoryMapped = 0x08,
        
        /// <summary>
        /// Used for standard lib in openedge version higher or equal than version 10.
        /// </summary>
        V11Standard = 0x0B,
        
        /// <summary>
        /// Used for memory mapped lib in openedge version higher or equal than version 10.
        /// </summary>
        V12MemoryMapped = 0x0C
    }
}