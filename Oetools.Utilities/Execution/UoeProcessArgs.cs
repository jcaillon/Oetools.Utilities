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
using System.Text;
using DotUtilities;
using DotUtilities.ParameterString;
using DotUtilities.Process;
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
                        tokens.RemoveAt(tokens.Count - 1);
                        return AppendFromPfFilePath(arg);
                    }
                    return base.Append(arg);
                }
                _lastAppendWasParameterFile = arg.Equals("-pf", StringComparison.Ordinal);
            } else {
                _lastAppendWasParameterFile = false;
            }
            return base.Append(arg);
        }

        /// <summary>
        /// Reads arguments from a progress parameter file.
        /// </summary>
        /// <param name="pfPath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public UoeProcessArgs AppendFromPfFilePath(string pfPath, Encoding encoding = null) {
            if (!File.Exists(pfPath)) {
                return null;
            }
            var tokenizer = UoePfTokenizer.New(File.ReadAllText(pfPath, encoding ?? TextEncodingDetect.GetFileEncoding(pfPath)));
            while (tokenizer.MoveToNextToken()) {
                var token = tokenizer.PeekAtToken(0);
                if (token is ParameterStringTokenOption || token is ParameterStringTokenValue) {
                    Append(token.Value);
                }
            }
            return this;
        }
    }
}
