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
    public class OeExecutionDbExtractTableAndSequenceList : OeExecutionDbExtract {
        
        public OeExecutionDbExtractTableAndSequenceList( IEnvExecution env) : base(env) { }

        public override string DatabaseExtractCandoTblType { get; set; } = "T,S";

        public override string DatabaseExtractCandoTblName { get; set; } = "_Sequence,!_*,*";

        /// <summary>
        ///     Get a list with all the tables + CRC
        /// </summary>
        /// <returns></returns>
        public List<TableCrc> GetTableCrc() {
            var output = new List<TableCrc>();
            Utils.ForEachLine(_databaseExtractFilePath,null, (i, line) => {
                var split = line.Split('\t');
                if (split.Length == 2)
                    output.Add(new TableCrc {
                        QualifiedTableName = split[0],
                        Crc = split[1]
                    });
            }, Encoding.Default);
            return output;
        }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            
            SetPreprocessedVar("DatabaseExtractCandoTblType", DatabaseExtractCandoTblType.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractCandoTblName", DatabaseExtractCandoTblName.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractFilePath", _databaseExtractFilePath.ProPreProcStringify());
        }

        protected override void AppendProgramToRun(StringBuilder runnerProgram) {
            base.AppendProgramToRun(runnerProgram);
            runnerProgram.AppendLine(ProgramDumpTableAndSequence);
        }
        
        private string ProgramDumpTableAndSequence => OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution_extract_db.p");
    }
}