namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    /// Error found when compiling a file
    /// </summary>
    public class CompilationError {

        /// <summary>
        /// Path of the file in which we found the error
        /// (can be different from the actual compiled file if the error is in an include)
        /// </summary>
        public string SourcePath { get; set; }

        public CompilationErrorLevel Level { get; set; }
        
        /// <summary>
        /// Line starts at 1
        /// </summary>
        public int Line { get; set; }
        public int Column { get; set; }
        public int ErrorNumber { get; set; }
        public string Message { get; set; }
    }

    public enum CompilationErrorLevel {
        Warning,
        Error
    }
}