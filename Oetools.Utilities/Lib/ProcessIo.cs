#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProcessIo.cs) is part of Oetools.Utilities.
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
using System.Diagnostics;
using System.Text;
using Oetools.Utilities.Lib.Extension;

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
        /// Choose the encoding for the standard/error output
        /// </summary>
        public virtual Encoding RedirectedOutputEncoding { get; set; }

        /// <summary>
        /// Standard output, to be called after the process exits
        /// </summary>
        public StringBuilder StandardOutput {
            get {
                if (_standardOutput == null || _process != null && !_process.HasExited) {
                    _standardOutput = new StringBuilder();
                    foreach (var s in StandardOutputArray) {
                        _standardOutput.AppendLine(s);
                    }
                    _standardOutput.TrimEnd();
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
                        _errorOutput.AppendLine(s);
                    }
                    _errorOutput.TrimEnd();
                }
                return _errorOutput;
            }
        }
        
        private StringBuilder _batchModeOutput;
        
        /// <summary>
        /// Returns all the messages sent to the standard or error output, should be used once the process has exited
        /// </summary>
        public StringBuilder BatchOutput {
            get {
                if (_batchModeOutput == null || _process != null && !_process.HasExited) {
                    _batchModeOutput = new StringBuilder();
                    if (ErrorOutputArray.Count > 0) {
                        foreach (var s in ErrorOutputArray) {
                            _batchModeOutput.AppendLine(s);
                        }
                        _batchModeOutput.TrimEnd();
                    }

                    if (StandardOutputArray.Count > 0) {
                        foreach (var s in StandardOutputArray) {
                            _batchModeOutput.AppendLine(s);
                        }
                        _batchModeOutput.TrimEnd();
                    }
                }
                return _batchModeOutput;
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

        private int? _exitCode;
        
        /// <summary>
        /// Exit code of the process
        /// </summary>
        public int ExitCode {
            get {
                if (!_exitCode.HasValue && _process != null) {
                    _process.WaitForExit();
                    _exitCode = _process.ExitCode;
                }
                return _exitCode ?? 0;
            }
            set { _exitCode = value; }
        }

        
        /// <summary>
        /// Whether or not this process has been killed
        /// </summary>
        public bool Killed { get; private set; }

        protected ProcessStartInfo _startInfo;
        
        protected Process _process;

        private StringBuilder _standardOutput;

        private bool _exitedEventPublished;

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
                return Execute(arguments, silent) && ErrorOutputArray.Count == 0;
            } catch (Exception e) {
                ErrorOutputArray.Add(e.ToString());
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
            if (!_process?.HasExited ?? false) {
                _process?.Kill();
            }
        }

        /// <summary>
        /// Returns true if the process has exited (can be false if timeout was reached)
        /// </summary>
        /// <param name="timeoutMs"></param>
        /// <returns></returns>
        protected virtual bool WaitUntilProcessExits(int timeoutMs) {
            if (_process == null) {
                return true;
            }

            if (timeoutMs > 0) {
                var exited = _process.WaitForExit(timeoutMs);
                if (!exited) {
                    return false;
                }
            } else {
                _process.WaitForExit();
            }

            ExitCode = _process.ExitCode;

            _process?.Close();
            _process?.Dispose();
            _process = null;

            return true;
        }

        protected virtual void PrepareStart(string arguments, bool silent) {
            _exitedEventPublished = false;
            StandardOutputArray.Clear();
            _standardOutput = null;
            ErrorOutputArray.Clear();
            _errorOutput = null;
            _batchModeOutput = null;
            Killed = false;
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
                //_startInfo.CreateNoWindow = true;
            }

            if (RedirectOutput) {
                _startInfo.RedirectStandardError = true;
                _startInfo.RedirectStandardOutput = true;
                if (RedirectedOutputEncoding != null) {
                    _startInfo.StandardErrorEncoding = RedirectedOutputEncoding;
                    _startInfo.StandardOutputEncoding = RedirectedOutputEncoding;
                }
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
            if (!_exitedEventPublished) {
                // this boolean does not seem useful but i have seen weird behaviors where the
                // exited event is called twice when we WaitForExit(), better safe than sorry
                _exitedEventPublished = true;
                OnProcessExit?.Invoke(sender, e);
            }
        }
    }
}