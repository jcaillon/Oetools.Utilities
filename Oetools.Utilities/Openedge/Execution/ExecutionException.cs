﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ExecutionException.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Openedge.Execution {

    public class ExecutionException : Exception {
        public ExecutionException() { }
        public ExecutionException(string message) : base(message) { }
        public ExecutionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ExecutionKilledException : ExecutionException {
        public ExecutionKilledException(string message) : base(message) { }
    }
    
    /// <summary>
    /// Happens if there were an openedge runtime exception but we still managed to execute the process
    /// </summary>
    public class ExecutionOpenedgeException : ExecutionException {
        public int ErrorNumber { get; set; }
        public string ErrorMessage { get; set; }
        public override string Message => $"({ErrorNumber}) {ErrorMessage}";

        /// <summary>
        /// Get an exception from a formatted string "error (nb)",
        /// returns null if the format is incorrect
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static ExecutionOpenedgeException GetFromString(string input) {
            var idx = input.LastIndexOf('(');
            if (input.Length == 0 || 
                input[input.Length - 1] != ')' || 
                idx < 0 ||  
                input.Length - 1 - idx - 1 <= 0 || 
                !int.TryParse(input.Substring(idx + 1, input.Length - 1 - idx - 1), out int nb)) {
                nb = 0;
                idx = input.Length + 1;
            }
            if (nb > 0) {
                return new ExecutionOpenedgeException {
                    ErrorNumber = nb,
                    ErrorMessage = input.Substring(0, idx - 1)
                };
            }
            return null;
        }
    }
        
    /// <summary>
    /// Happens if there were an openedge database connection exception but we still managed to execute the process
    /// </summary>
    public class ExecutionOpenedgeDbConnectionException : ExecutionOpenedgeException {
    }
    
    /// <summary>
    /// Happens if the process failed to execute
    /// </summary>
    public class ExecutionProcessException : ExecutionException {
        
        public string ExecutablePath { get; set; }
        public string Parameters { get; set; }
        public string WorkingDirectory { get; set; }
        public string BatchModeOutput { get; set; }
        public int ExitCode { get; set; }
        
        public ExecutionProcessException(string executablePath, string parameters, string workingDirectory, string output, int exitCode) {
            ExecutablePath = executablePath;
            Parameters = parameters;
            WorkingDirectory = workingDirectory;
            BatchModeOutput = output;
            ExitCode = exitCode;
        }

        public override string Message => $"An error has occurred during the execution : {ExecutablePath} {Parameters}, in the directory : {WorkingDirectory}, exit code {ExitCode}{(!string.IsNullOrEmpty(BatchModeOutput) ? $", the output was {BatchModeOutput}" : "")}";
    }

    public class ExecutionParametersException : ExecutionException {
        public ExecutionParametersException() { }
        public ExecutionParametersException(string message) : base(message) { }
        public ExecutionParametersException(string message, Exception innerException) : base(message, innerException) { }
    }
}