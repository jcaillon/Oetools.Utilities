#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionDbExtract.cs) is part of Oetools.Utilities.
//
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.IO;
using System.Text;
using Oetools.Utilities.Openedge.Execution.Exceptions;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    ///     Allows to output a file containing the structure of the database
    /// </summary>
    public abstract class AUoeExecutionDbExtract : AUoeExecution {

        /// <inheritdoc />
        public override bool NeedDatabaseConnection => true;

        protected override bool ForceCharacterModeUse => true;

        /// <summary>
        /// Only the tables with a type that CAN-DO this value will be fetched.
        /// Below is the equivalent progress code that will be used :
        /// <c>CAN-DO(this_value, DB._FILE._Tbl-Type)</c>
        /// </summary>
        /// <example>
        /// For instance, you can use the following value to fetch ALL the tables :*
        /// Or just the user and system tables :T,S
        /// Here is the list of all the possible values :
        /// - T : User Data Table
        /// - S : Virtual System Table
        /// - V : SQL View
        /// </example>
        public virtual string DatabaseExtractCandoTblType { get; set; } = "T";

        /// <summary>
        /// Only the tables with a name that CAN-DO this value will be fetched.
        /// Below is the equivalent progress code that will be used :
        /// <c>CAN-DO(this_value, DB._FILE._FILE-NAME)</c>
        /// </summary>
        /// <example>
        /// Here is an example to fetch all user tables but only 4 particular system tables:_Sequence,_FILE,_INDEX,_FIELD,!_*,*
        /// You have to set T,S in the table type CAN-DO for this example...
        /// </example>
        public virtual string DatabaseExtractCandoTblName { get; set; } = "*";

        protected virtual string DatabaseExtractType => null;

        protected virtual string DatabaseExtractExternalProgramPath => null;

        protected string _databaseExtractFilePath;

        public AUoeExecutionDbExtract(AUoeExecutionEnv env) : base(env) {
            _databaseExtractFilePath = Path.Combine(_tempDir, "db.dump");
        }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();

            SetPreprocessedVar("DatabaseExtractCandoTblType", DatabaseExtractCandoTblType.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractCandoTblName", DatabaseExtractCandoTblName.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractFilePath", _databaseExtractFilePath.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractType", DatabaseExtractType.ProPreProcStringify());
            SetPreprocessedVar("DatabaseExtractExternalProgramPath", DatabaseExtractExternalProgramPath.ProPreProcStringify());
        }

        /// <summary>
        /// Get the results
        /// </summary>
        protected override void GetProcessResults() {
            base.GetProcessResults();

            // end of successful execution action
            if (!ExecutionFailed) {
                try {
                    ReadExtractionResults();
                } catch (Exception e) {
                    HandledExceptions.Add(new UoeExecutionException("Error while reading the compilation results.", e));
                }
            }
        }

        /// <summary>
        /// Method called at the end of the execution, should be used to read the extraction file.
        /// </summary>
        protected abstract void ReadExtractionResults();

        protected override void AppendProgramToRun(StringBuilder runnerProgram) {
            base.AppendProgramToRun(runnerProgram);
            runnerProgram.AppendLine(OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution_extract_db.p"));
        }

    }
}
