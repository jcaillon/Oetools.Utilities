using Oetools.Utilities.Resources;

namespace Oetools.Builder.Core2.Execution {
    
    public class DatabaseExtractionOptions : IDatabaseExtractionOptions {

        public DatabaseExtractionOptions() {
            DatabaseExtractCandoTblType = "T";
            DatabaseExtractCandoTblName = "*";
        }

        public string DatabaseExtractCandoTblType { get; set; }
        public string DatabaseExtractCandoTblName { get; set; }

    }
}