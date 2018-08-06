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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Oetools.Utilities.Lib {

    public class ProcessIo {
        
        /// <summary>
        /// The full path to the executable used
        /// </summary>
        public string ExecutablePath { get; set; }
        
        /// <summary>
        /// Subscribe to this event called when the process exits
        /// </summary>
        public event EventHandler<EventArgs> OnProcessExit;
        
        /// <summary>
        /// The working directory to use for this process
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Choose to redirect the standard/error output or no, default to true
        /// </summary>
        public bool RedirectOutput { get; set; } = true;

        /// <summary>
        /// Standard output, to be called after the process exits
        /// </summary>
        public StringBuilder StandardOutput {
            get {
                if (_standardOutput == null || _process != null && !_process.HasExited) {
                    _standardOutput = new StringBuilder();
                    foreach (var s in StandardOutputArray) {
                        _standardOutput.Append(s);
                    }
                }
                return _standardOutput;
            }
        }
        
        private StringBuilder _errorOutput;

        /// <summary>
        /// Error output, to be called after the process exits
        /// </summary>
        public StringBuilder ErrorOutput {
            get {
                if (_errorOutput == null || _process != null && !_process.HasExited) {
                    _errorOutput = new StringBuilder();
                    foreach (var s in ErrorOutputArray) {
                        _errorOutput.Append(s);
                    }
                }
                return _errorOutput;
            }
        }
        
        /// <summary>
        /// Standard output, to be called after the process exits
        /// </summary>
        public List<string> StandardOutputArray { get; private set; } = new List<string>();

        /// <summary>
        /// Error output, to be called after the process exits
        /// </summary>
        public List<string> ErrorOutputArray { get; private set; } = new List<string>();

        /// <summary>
        /// Exit code of the process
        /// </summary>
        public int ExitCode { get; private set; }
        
        /// <summary>
        /// Whether or not this process has been killed
        /// </summary>
        public bool Killed { get; private set; }

        protected ProcessStartInfo _startInfo;
        
        protected Process _process;

        private StringBuilder _standardOutput;

        /// <summary>
        ///     Constructor
        /// </summary>
        public ProcessIo(string executablePath) {
            ExecutablePath = executablePath;
        }

        /// <summary>
        /// Start the process synchronously, catch the exceptions
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
        /// Start the process synchronously
        /// </summary>
        public bool Execute(string arguments = null, bool silent = true, int timeoutMs = 0) {
            ExecuteAsyncProcess(arguments, silent);

            WaitUntilProcessExits(timeoutMs);

            return ExitCode == 0;
        }

        /// <summary>
        /// Start the process asynchronously, use <see cref="OnProcessExit"/> event to know when the process is done
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

        /// <summary>
        /// Kill the process
        /// </summary>
        public void Kill() {
            Killed = true;
            _process?.Kill();
        }

        protected virtual void WaitUntilProcessExits(int timeoutMs) {
            if (_process == null) {
                return;
            }
            
            if (timeoutMs > 0) {
                _process.WaitForExit(timeoutMs);
            } else {
                _process.WaitForExit();
            }

            ExitCode = _process.ExitCode;

            _process?.Close();
            _process?.Dispose();
            _process = null;
        }

        protected virtual void PrepareStart(string arguments, bool silent) {
            StandardOutputArray.Clear();
            ErrorOutputArray.Clear();
            ExitCode = 0;

            _startInfo = new ProcessStartInfo {
                FileName = ExecutablePath,
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
            if (!string.IsNullOrEmpty(args.Data)) {
                ErrorOutputArray.Add(args.Data);
            }
        }

        protected virtual void OnProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args) {
            if (!string.IsNullOrEmpty(args.Data)) {
                StandardOutputArray.Add(args.Data);
            }
        }

        protected virtual void ProcessOnExited(object sender, EventArgs e) {
            if (_process != null) {
                ExitCode = _process.ExitCode;
            }
            OnProcessExit?.Invoke(sender, e);
        }
    }
}