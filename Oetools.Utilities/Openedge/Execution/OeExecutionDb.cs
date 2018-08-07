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
    public abstract class OeExecutionDb : OeExecution {
        
        protected override bool ForceCharacterModeUse => true;

        public string DatabaseExtractCandoTblType { get; set; } = "T";
        
        public string DatabaseExtractCandoTblName { get; set; } = "*";

        public OeExecutionDb(IEnvExecution env) : base(env) { }

    }
}