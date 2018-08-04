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
        public string WorkingDirectory { get; set; }

        public StringBuilder StandardOutput { get; }

        public StringBuilder ErrorOutput { get; }

        public int ExitCode { get; private set; }

        public ProcessStartInfo StartInfo { get; }

        public bool WaitForExit { get; set; }

        /// <summary>
        ///     Constructor
        /// </summary>
        public ProcessIo(string executable, bool hidden = true) {
            StandardOutput = new StringBuilder();
            ErrorOutput = new StringBuilder();
            StartInfo = new ProcessStartInfo {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true
            };
            if (hidden) {
                StartInfo.CreateNoWindow = true;
                StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
        }

        /// <summary>
        /// Start the process synchronously, catch the exceptions
        /// </summary>
        public bool TryExecute(string arguments = null) {
            try {
                return Execute(arguments) && ErrorOutput.Length == 0;
            } catch (Exception e) {
                ErrorOutput.AppendLine(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Start the process synchronously
        /// </summary>
        public bool Execute(string arguments = null) {
            StandardOutput.Clear();
            ErrorOutput.Clear();
            ExitCode = 0;

            if (!string.IsNullOrEmpty(arguments)) {
                StartInfo.Arguments = arguments;
            }

            if (!string.IsNullOrEmpty(WorkingDirectory)) {
                StartInfo.WorkingDirectory = WorkingDirectory;
            }

            using (var process = new Process {
                StartInfo = StartInfo
            }) {
                process.OutputDataReceived += OnProcessOnOutputDataReceived;
                process.ErrorDataReceived += OnProcessOnErrorDataReceived;

                process.Start();

                // Asynchronously read the standard output of the spawned process
                // This raises OutputDataReceived events for each line of output
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                ExitCode = process.ExitCode;

                process.Close();

                ErrorOutput.TrimEnd();
                StandardOutput.TrimEnd();
            }

            return ExitCode == 0;
        }

        private void OnProcessOnErrorDataReceived(object sender, DataReceivedEventArgs args) {
            ErrorOutput.AppendLine(args.Data);
        }

        private void OnProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args) {
            StandardOutput.AppendLine(args.Data);
        }
    }
}