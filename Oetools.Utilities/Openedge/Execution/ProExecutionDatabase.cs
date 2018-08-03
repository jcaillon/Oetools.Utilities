using System;
using System.IO;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Resources;

namespace Oetools.Packager.Core2.Execution {
    
    /// <summary>
    /// Allows to output a file containing the structure of the database
    /// </summary>
    internal class ProExecutionDatabase : ProExecution {

        /// <summary>
        ///     Copy of the pro env to use
        /// </summary>
        public IDatabaseExtractionOptions Config { get; private set; }

        public ProExecutionDatabase(IDatabaseExtractionOptions config, IEnvExecution env) : base(env) {
            Config = config;
        }

        protected override ExecutionType ExecutionType => ExecutionType.Database;

        /// <summary>
        /// File to the output path that contains the structure of the database
        /// </summary>
        public string OutputPath { get; set; }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();

            OutputPath = Path.Combine(_localTempDir, "db.extract");
            var fileToExecute = "db_" + DateTime.Now.ToString("yyMMdd_HHmmssfff") + ".p";

            SetPreprocessedVar("OutputPath", OutputPath.PreProcQuoter());
            SetPreprocessedVar("CurrentFilePath", fileToExecute.PreProcQuoter());

            try {
                File.WriteAllText(Path.Combine(_localTempDir, fileToExecute), ProgramDumpDatabase);
            } catch (Exception e) {
                throw new ExecutionParametersException("Couldn't start an execution, couldn't create the dump database program file : " + e.Message, e);
            }
        }
        
        protected override bool SilentExecution => true;
        
        protected override bool CanUseBatchMode => true;

        private string ProgramDumpDatabase => OpenedgeResources.GetOpenedgeAsStringFromResources(@"DumpDatabase.p");

    }
}