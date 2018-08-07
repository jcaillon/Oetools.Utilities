using System;

namespace Oetools.Utilities.Openedge.Execution {

    public class ExecutionException : Exception {
        public ExecutionException() { }
        public ExecutionException(string message) : base(message) { }
        public ExecutionException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// Happens if there were an openedge runtime exception but we still managed to execute the process
    /// </summary>
    public class ExecutionOpenedgeException : ExecutionException {
        public int ErrorNumber { get; set; }
        public string ErrorMessage { get; set; }
        public override string Message => $"({ErrorNumber}) {ErrorMessage}";
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