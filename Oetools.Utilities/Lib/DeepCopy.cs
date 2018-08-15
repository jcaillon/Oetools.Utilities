using System;

namespace Oetools.Utilities.Lib {
    /// <summary>
    /// Special attribute that allows to decide wether or not properties should be written when using object deep copy
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DeepCopy : Attribute {
            
        /// <summary>
        /// Do not replace the value of this property
        /// </summary>
        public bool Ignore { get; set; }
            
        public DeepCopy() { }
    }
}