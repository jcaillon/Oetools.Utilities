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
    public class OeExecutionDbExtractTableCrc : OeExecutionDb {
        
        public OeExecutionDbExtractTableCrc( IEnvExecution env) : base(env) { }

        /// <summary>
        ///     Get a list with all the tables + CRC
        /// </summary>
        /// <returns></returns>
        public List<TableCrc> GetTableCrc() {
            var output = new List<TableCrc>();
            Utils.ForEachLine(OutputPath, new byte[0], (i, line) => {
                var split = line.Split('\t');
                if (split.Length == 2)
                    output.Add(new TableCrc {
                        QualifiedTableName = split[0],
                        Crc = split[1]
                    });
            }, Encoding.Default);
            return output;
        }

        /// <summary>
        /// File to the output path that contains the CRC of each table
        /// </summary>
        public string OutputPath { get; set; }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();

            OutputPath = Path.Combine(_tempDir, "db.extract");
            SetPreprocessedVar("OutputPath", OutputPath.ProPreProcStringify());

            var fileToExecute = "db_" + DateTime.Now.ToString("yyMMdd_HHmmssfff") + ".p";
            File.WriteAllText(Path.Combine(_tempDir, fileToExecute), ProgramDumpTableCrc);
            SetPreprocessedVar("CurrentFilePath", fileToExecute.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractCandoTblType", DatabaseExtractCandoTblType.Trim().ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractCandoTblName", DatabaseExtractCandoTblName.Trim().ProPreProcStringify());
        }
        
        private string ProgramDumpTableCrc => OpenedgeResources.GetOpenedgeAsStringFromResources(@"DumpTableCrc.p");
    }
}