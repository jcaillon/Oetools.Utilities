#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionKilledException.cs) is part of Oetools.Utilities.
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

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oetools.Utilities.Openedge.Execution.Exceptions {

    /// <summary>
    /// Thrown on compilation error or warning.
    /// </summary>
    public class UoeExecutionCompilationStoppedException : UoeExecutionOpenedgeException {

        /// <summary>
        /// The execution should stop when a compilation warning is encountered.
        /// </summary>
        public bool StopOnWarning { get; set; }

        /// <summary>
        /// A list of all the compilation issues.
        /// </summary>
        public List<AUoeCompilationProblem> CompilationProblems { get; set; }

        public override string Message {
            get {
                var sb = new StringBuilder("The compilation process stopped on the first compilation ").Append(StopOnWarning ? "warning" : "error").Append(": ");
                if (CompilationProblems != null && CompilationProblems.Count > 0) {
                    foreach (var filePathGrouped in CompilationProblems.GroupBy(cp => cp.FilePath)) {
                        sb.AppendLine();
                        sb.Append("in ").Append(filePathGrouped.Key).Append(":");
                        foreach (var problem in filePathGrouped) {
                            sb.AppendLine();
                            sb.Append(" - ").Append(problem);
                        }
                    }
                } else {
                    sb.Append("empty problem list.");
                }
                return sb.ToString();
            }
        }
    }
}
