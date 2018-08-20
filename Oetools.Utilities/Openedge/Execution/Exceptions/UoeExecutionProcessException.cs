#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionProcessException.cs) is part of Oetools.Utilities.
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
namespace Oetools.Utilities.Openedge.Execution.Exceptions {
    /// <summary>
    /// Happens if the process failed to execute
    /// </summary>
    public class UoeExecutionProcessException : UoeExecutionException {
        
        public string ExecutablePath { get; set; }
        public string Parameters { get; set; }
        public string WorkingDirectory { get; set; }
        public string BatchModeOutput { get; set; }
        public int ExitCode { get; set; }
        
        public UoeExecutionProcessException(string executablePath, string parameters, string workingDirectory, string output, int exitCode) {
            ExecutablePath = executablePath;
            Parameters = parameters;
            WorkingDirectory = workingDirectory;
            BatchModeOutput = output;
            ExitCode = exitCode;
        }

        public override string Message => $"An error has occurred during the execution : {ExecutablePath} {Parameters}, in the directory : {WorkingDirectory}, exit code {ExitCode}{(!string.IsNullOrEmpty(BatchModeOutput) ? $", the output was {BatchModeOutput}" : "")}";
    }
}