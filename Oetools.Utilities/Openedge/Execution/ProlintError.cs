namespace Oetools.Builder.Core2.Execution {
    
    /// <summary>
    ///     Errors found for this file, either from compilation or from prolint
    /// </summary>
    public class ProlintError {

        /// <summary>
        ///     The path to the file that was compiled to generate this error (you can compile a .p and have the error on a .i)
        /// </summary>
        public string CompiledFilePath { get; set; }

        /// <summary>
        ///     Path of the file in which we found the error
        /// </summary>
        public string SourcePath { get; set; }

        public ProlintErrorLevel Level { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int ErrorNumber { get; set; }
        public string Message { get; set; }

        /// <summary>
        ///     indicates if the error appears several times
        /// </summary>
        public int Times { get; set; }
    }

    /// <summary>
    ///     Describes the error level, the num is also used for MARKERS in scintilla
    ///     and thus must start at 0
    /// </summary>
    public enum ProlintErrorLevel {
        Information,
        Warning,
        StrongWarning,
        Error,
        Critical
    }
}