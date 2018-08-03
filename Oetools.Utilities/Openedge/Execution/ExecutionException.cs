using System;

namespace Oetools.Packager.Core2.Execution {
    public class ExecutionException : Exception {
        public ExecutionException() { }
        public ExecutionException(string message) : base(message) { }
        public ExecutionException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    public class ExecutionProcessException : Exception {
        
        public string ExecutablePath { get; set; }
        public string Parameters { get; set; }
        public string WorkingDirectory { get; set; }
        public string Output { get; set; }
        
        public ExecutionProcessException(string executablePath, string parameters, string workingDirectory, string output) {
            ExecutablePath = executablePath;
            Parameters = parameters;
            WorkingDirectory = workingDirectory;
            Output = output;
        }
        public ExecutionProcessException(string message, string executablePath, string parameters, string workingDirectory, string output) : base(message) {
            ExecutablePath = executablePath;
            Parameters = parameters;
            WorkingDirectory = workingDirectory;
            Output = output;
        }
        public ExecutionProcessException(string message, Exception innerException, string executablePath, string parameters, string workingDirectory, string output) : base(message, innerException) {
            ExecutablePath = executablePath;
            Parameters = parameters;
            WorkingDirectory = workingDirectory;
            Output = output;
        }
    }

    public class ExecutionParametersException : ExecutionException {
        public ExecutionParametersException() { }
        public ExecutionParametersException(string message) : base(message) { }
        public ExecutionParametersException(string message, Exception innerException) : base(message, innerException) { }
    }
}