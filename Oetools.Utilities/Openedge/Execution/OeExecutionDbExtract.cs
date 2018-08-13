using System.IO;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    ///     Allows to output a file containing the structure of the database
    /// </summary>
    public abstract class OeExecutionDbExtract : OeExecution {

        public override bool NeedDatabaseConnection => true;
        
        protected override bool ForceCharacterModeUse => true;

        public virtual string DatabaseExtractCandoTblType { get; set; } = "T";
        
        public virtual string DatabaseExtractCandoTblName { get; set; } = "*";

        protected string _databaseExtractFilePath;

        public OeExecutionDbExtract(IEnvExecution env) : base(env) {
            _databaseExtractFilePath = Path.Combine(_tempDir, "db.dump");
        }

    }
}