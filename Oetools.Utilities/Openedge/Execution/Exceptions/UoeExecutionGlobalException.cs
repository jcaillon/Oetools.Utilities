#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionGlobalException.cs) is part of Oetools.Utilities.
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
using System.Collections.Generic;
using System.Text;

namespace Oetools.Utilities.Openedge.Execution.Exceptions {

    /// <summary>
    /// Thrown when the execution has failed, regroup the different exceptions.
    /// </summary>
    public class UoeExecutionGlobalException : Exception {

        /// <summary>
        /// The exceptions that occured during the execution.
        /// </summary>
        public List<UoeExecutionException> HandledExceptions { get; }

        internal UoeExecutionGlobalException(List<UoeExecutionException> handledExceptions) {
            HandledExceptions = handledExceptions;
        }

        /// <inheritdoc />
        public override string Message {
            get {
                var sb = new StringBuilder("Compiler exceptions: ");
                if (HandledExceptions != null && HandledExceptions.Count > 0) {
                    foreach (var ex in HandledExceptions) {
                        sb.AppendLine();
                        sb.Append("* ").Append(ex.Message);
                    }
                } else {
                    sb.Append("empty exception list.");
                }
                return sb.ToString();
            }
        }
    }
}
