namespace Oetools.Utilities.Openedge.Execution {
    
    public class DatabaseExtractionOptions : IDatabaseExtractionOptions {

        public DatabaseExtractionOptions() {
            DatabaseExtractCandoTblType = "T";
            DatabaseExtractCandoTblName = "*";
        }

        public string DatabaseExtractCandoTblType { get; set; }
        public string DatabaseExtractCandoTblName { get; set; }

    }
}