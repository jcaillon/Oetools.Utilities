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

namespace Oetools.Utilities.Openedge.Execution.Exceptions {
    public class UoeExecutionCompilationStoppedException : UoeExecutionOpenedgeException {
                
        public bool StopOnWarning { get; set; }
        
        public List<UoeCompilationProblem> CompilationProblems { get; set; }
        
        public override string Message => $"The compilation process stopped on the first compilation {(StopOnWarning ? "warning" : "error")}{(CompilationProblems != null && CompilationProblems.Count > 0 ? $" :\n- {string.Join("\n- ", CompilationProblems)}" : "")}";
    }
}