#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeCompilationProblem.cs) is part of Oetools.Utilities.
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
using System.IO;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    /// Error found when compiling a file
    /// </summary>
    public abstract class UoeCompilationProblem {

        public static UoeCompilationProblem New(CompilationErrorLevel compilationErrorLevel) {
            switch (compilationErrorLevel) {
                case CompilationErrorLevel.Warning:
                    return new UoeCompilationWarning();
                case CompilationErrorLevel.Error:
                    return new UoeCompilationError();
                default:
                    throw new ArgumentOutOfRangeException(nameof(compilationErrorLevel), compilationErrorLevel, null);
            }
        }

        /// <summary>
        /// Path of the file in which we found the error
        /// (can be different from the actual compiled file if the error is in an include)
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// Line starts at 1
        /// </summary>
        public int Line { get; set; }
        public int Column { get; set; }
        public int ErrorNumber { get; set; }
        public string Message { get; set; }

        public override string ToString() => $"Line {Line}, Column {Column}, error {ErrorNumber} : {(string.IsNullOrEmpty(FilePath) ? Message : Message.Replace(FilePath, Path.GetFileName(FilePath)))}";
    }
}