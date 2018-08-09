namespace Oetools.Utilities.Openedge {
    
    /// <summary>
    /// Represent the tables or sequences that were referenced in a given .r code file and thus needed to compile
    /// also, if one reference changes, the file should be recompiled
    /// </summary>
    public class DatabaseReference {
        public virtual string QualifiedName { get; set; }
    }
    
    public class DatabaseReferenceSequence : DatabaseReference {
    }
    
    public class DatabaseReferenceTable : DatabaseReference {
        public virtual string Crc { get; set; }
    }
}