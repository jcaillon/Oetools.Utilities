#region header

// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProcessIoWithLog.cs) is part of Oetools.Utilities.
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

using System.Diagnostics;
using System.Text;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Lib {

    /// <summary>
    /// Wrapper for process with logging ability.
    /// </summary>
    public class ProcessIoWithLog : ProcessIo {
        /// <summary>
        /// A logger.
        /// </summary>
        public ILog Log { get; set; }

        public ProcessIoWithLog(string executablePath) : base(executablePath) { }

        protected override void PrepareStart(string arguments, bool silent) {
            base.PrepareStart(arguments, silent);
            _batchOutput = null;
            Log?.Debug($"Executing command:\n{ExecutedCommandLine}");
        }

        private string _batchOutput;

        /// <summary>
        /// Returns all the messages sent to the standard or error output, should be used once the process has exited
        /// </summary>
        public string BatchOutputString {
            get {
                if (_batchOutput == null) {
                    var batchModeOutput = new StringBuilder();
                    foreach (var s in ErrorOutputArray.ToNonNullEnumerable()) {
                        batchModeOutput.AppendLine(s.Trim());
                    }

                    foreach (var s in StandardOutputArray.ToNonNullEnumerable()) {
                        batchModeOutput.AppendLine(s.Trim());
                    }

                    _batchOutput = batchModeOutput.ToString();
                }

                return _batchOutput;
            }
        }

        //public override bool Execute(string arguments = null, bool silent = true, int timeoutMs = 0) {
        //    var result = base.Execute(arguments, silent, timeoutMs);
        //    Log?.Debug($"Command output:\n{BatchOutputString}");
        //    return result;
        //}

        protected override void OnProcessOnErrorDataReceived(object sender, DataReceivedEventArgs args) {
            base.OnProcessOnErrorDataReceived(sender, args);
            var line = args?.Data?.Trim();
            if (!string.IsNullOrEmpty(line)) {
                Log?.Debug(line);
            }
        }

        protected override void OnProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args) {
            base.OnProcessOnOutputDataReceived(sender, args);
            var line = args?.Data?.Trim();
            if (!string.IsNullOrEmpty(line)) {
                Log?.Debug(line);
            }
        }
    }
}
