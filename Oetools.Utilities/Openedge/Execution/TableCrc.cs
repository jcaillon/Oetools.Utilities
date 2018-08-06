namespace Oetools.Utilities.Openedge.Execution {
    /// <summary>
    ///     This class represent the tables that were referenced in a given .r code file
    /// </summary>[Serializable]
    public class TableCrc {
        public string QualifiedTableName { get; set; }
        public string Crc { get; set; }
    }
}