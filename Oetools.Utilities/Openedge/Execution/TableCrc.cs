namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    ///     This class represent the tables that were referenced in a given .r code file
    /// </summary>
    public class TableCrc {
        public virtual string QualifiedTableName { get; set; }
        public virtual string Crc { get; set; }
    }
}