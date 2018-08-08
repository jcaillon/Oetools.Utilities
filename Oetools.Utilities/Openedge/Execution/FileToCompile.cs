// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileToCompile.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    ///     This class represents a file that needs to be compiled
    /// </summary>
    public class FileToCompile {
        
        private string _compilePath;

        /// <summary>
        /// The path to the source file
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// The path of the file that actually needs to be compiled
        /// (can be different from sourcepath if we edited it without saving it for instance)
        /// </summary>
        public string CompiledPath {
            get => _compilePath ?? SourcePath;
            set => _compilePath = value;
        }

        /// <summary>
        /// Directory in which to compile this file, if null a temporary directory will be used
        /// </summary>
        public string PreferedTargetPath { get; set; }

        public FileToCompile(string sourcePath) {
            SourcePath = sourcePath;
        }
    }

}