using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    ///     Allows to output a file containing the structure of the database
    /// </summary>
    public abstract class OeExecutionDbExtract : OeExecution {
        
        protected override bool ForceCharacterModeUse => true;

        public virtual string DatabaseExtractCandoTblType { get; set; } = "T";
        
        public virtual string DatabaseExtractCandoTblName { get; set; } = "*";

        protected string _databaseExtractFilePath;

        public OeExecutionDbExtract(IEnvExecution env) : base(env) {
            _databaseExtractFilePath = Path.Combine(_tempDir, "db.dump");
        }

    }
}