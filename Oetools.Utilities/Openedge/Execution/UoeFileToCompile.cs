#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeFileToCompile.cs) is part of Oetools.Utilities.
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

using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    ///     This class represents a file that needs to be compiled
    /// </summary>
    public class UoeFileToCompile : IFileListItem {
        
        private string _compilePath;

        /// <summary>
        /// The path to the source file
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// The path of the file that actually needs to be compiled
        /// (can be different from sourcepath if we edited it without saving it for instance)
        /// </summary>
        public string CompiledPath {
            get => _compilePath ?? SourceFilePath;
            set => _compilePath = value;
        }

        /// <summary>
        /// Directory in which to compile this file, if null a temporary directory will be used
        /// </summary>
        public string PreferedTargetDirectory { get; set; }
        
        /// <summary>
        /// Size of the file to compile, can be left to 0, used in parallel compilation to try to
        /// get a better repartition of files accross processes
        /// </summary>
        public long FileSize { get; set; }

        public UoeFileToCompile(string sourceFilePath) {
            SourceFilePath = sourceFilePath;
        }
    }

}