﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProcessIoAsync.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Lib {
    
    public class ProcessIoAsync : ProcessIo, IDisposable {
        
        public ProcessIoAsync(string executable) : base(executable) { }
        
        /// <summary>
        /// Start the process, use <see cref="ProcessIo.OnProcessExit"/> event to know when the process is done
        /// </summary>
        public void ExecuteAsync(string arguments = null, bool silent = true) {
            ExecuteAsyncProcess(arguments, silent);
        }

        public void WaitForExit(int timeoutMs = 0) {
            WaitUntilProcessExits(timeoutMs);
        }

        public void Dispose() {
            _process?.Close();
            _process?.Dispose();
            _process = null;
        }
    }
}