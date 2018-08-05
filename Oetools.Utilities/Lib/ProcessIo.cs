// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProcessIo.cs) is part of csdeployer.
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

using System;
using System.Diagnostics;
using System.Text;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Lib {

    public class ProcessIo {
        
        public string Executable { get; set; }
        
        public event EventHandler<EventArgs> OnProcessExit;
        
        public string WorkingDirectory { get; set; }

        public bool RedirectOutput { get; set; } = true;

        protected ProcessStartInfo _startInfo;
        
        protected Process _process;
        
        public StringBuilder StandardOutput { get; private set; }

        public StringBuilder ErrorOutput { get; private set; }

        public int ExitCode { get; private set; }
        
        public bool Killed { get; private set; }

        /// <summary>
        ///     Constructor
        /// </summary>
        public ProcessIo(string executable) {
            Executable = executable;
        }

        /// <summary>
        /// Start the process, catch the exceptions
        /// </summary>
        public bool TryExecute(string arguments = null, bool silent = true) {
            try {
                return Execute(arguments, silent) && ErrorOutput.Length == 0;
            } catch (Exception e) {
                ErrorOutput.AppendLine(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// Start the process
        /// </summary>
        public bool Execute(string arguments = null, bool silent = true, int timeoutMs = 0) {
            ExecuteAsyncProcess(arguments, silent);

            WaitUntilProcessExits(timeoutMs);

            return ExitCode == 0;
        }

        /// <summary>
        /// Start the process, use <see cref="OnProcessExit"/> event to know when the process is done
        /// </summary>
        protected virtual void ExecuteAsyncProcess(string arguments = null, bool silent = true) {
            PrepareStart(arguments, silent);

            _process.Start();

            if (RedirectOutput) {
                // Asynchronously read the standard output of the spawned process
                // This raises OutputDataReceived events for each line of output
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
        }

        public void Kill() {
            Killed = true;
            _process?.Kill();
        }

        protected virtual void WaitUntilProcessExits(int timeoutMs) {
            if (timeoutMs > 0) {
                _process.WaitForExit(timeoutMs);
            } else {
                _process.WaitForExit();
            }

            ExitCode = _process.ExitCode;

            _process?.Close();
            _process?.Dispose();
            _process = null;

            ErrorOutput.Trim();
            StandardOutput.Trim();
        }

        protected virtual void PrepareStart(string arguments, bool silent) {
            StandardOutput = new StringBuilder();
            ErrorOutput = new StringBuilder();
            ExitCode = 0;

            _startInfo = new ProcessStartInfo {
                FileName = Executable,
                UseShellExecute = false
            };

            if (!string.IsNullOrEmpty(arguments)) {
                _startInfo.Arguments = arguments;
            }

            if (!string.IsNullOrEmpty(WorkingDirectory)) {
                _startInfo.WorkingDirectory = WorkingDirectory;
            }

            if (silent) {
                _startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                _startInfo.CreateNoWindow = true;
            }

            if (RedirectOutput) {
                _startInfo.RedirectStandardError = true;
                _startInfo.RedirectStandardOutput = true;
            }

            _process = new Process {
                StartInfo = _startInfo
            };

            if (RedirectOutput) {
                _process.OutputDataReceived += OnProcessOnOutputDataReceived;
                _process.ErrorDataReceived += OnProcessOnErrorDataReceived;
            }

            if (OnProcessExit != null) {
                _process.EnableRaisingEvents = true;
                _process.Exited += ProcessOnExited;
            }
        }

        protected virtual void OnProcessOnErrorDataReceived(object sender, DataReceivedEventArgs args) {
            ErrorOutput.AppendLine(args.Data);
        }

        protected virtual void OnProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args) {
            StandardOutput.AppendLine(args.Data);
        }

        protected virtual void ProcessOnExited(object sender, EventArgs e) {
            ExitCode = _process.ExitCode;
            ErrorOutput.Trim();
            StandardOutput.Trim();
            OnProcessExit?.Invoke(sender, e);
        }
    }
}