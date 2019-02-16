#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeProcessArgs.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// A collection of arguments for an openedge process.
    /// If there are .pf files used, we try to read them to resolve the entire parameter string.
    /// </summary>
    public class UoeProcessArgs : ProcessArgs {

        private bool _lastAppendWasParameterFile;

        /// <inheritdoc />
        public override ProcessArgs Append(string arg) {
            if (!string.IsNullOrEmpty(arg)) {
                if (_lastAppendWasParameterFile) {
                    _lastAppendWasParameterFile = false;
                    if (File.Exists(arg)) {
                        // remove -pf option
                        items.RemoveAt(items.Count - 1);
                        return base.AppendFromQuotedArgs(UoeUtilities.GetConnectionStringFromPfFile(arg));
                    }
                    return base.Append(arg);
                }
                _lastAppendWasParameterFile = arg.Equals("-pf", StringComparison.Ordinal);
            } else {
                _lastAppendWasParameterFile = false;
            }
            return base.Append(arg);
        }

        /// <inheritdoc cref="ProcessArgs.Append(object[])"/>
        public virtual UoeProcessArgs Append2(params object[] args) {
            base.Append(args);
            return this;
        }
    }
}
