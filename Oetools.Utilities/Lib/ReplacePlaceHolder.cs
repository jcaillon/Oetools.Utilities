using System;

namespace Oetools.Utilities.Lib {
    /// <summary>
    /// Special attribute that allows to decide wether or not variables should be replaced in a property of type string
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ReplacePlaceHolder : Attribute {
            
        /// <summary>
        /// Do not replace the variables in this string property
        /// </summary>
        public bool SkipReplace { get; set; }
            
        public ReplacePlaceHolder() { }
    }
}