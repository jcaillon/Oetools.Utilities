﻿using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Oetools.Packager.Core2.Execution {
    
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
        /// Line starts at 0
        /// </summary>
        public int Line { get; set; }
        public int Column { get; set; }
        public int ErrorNumber { get; set; }
        public string Message { get; set; }
        public string Help { get; set; }

        /// <summary>
        ///     indicates if the error appears several times
        /// </summary>
        public int Times { get; set; }
    }

    public enum CompilationErrorLevel {
        Warning,
        Error
    }
}