#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionDbExtractTableAndSequenceList.cs) is part of Oetools.Utilities.
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
using System.Collections.Generic;
using System.IO;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// Get the datetime of the last update of databases schema.
    /// </summary>
    public class UoeExecutionDbExtractSchema : UoeExecutionDbExtract {

        public UoeExecutionDbExtractSchema(AUoeExecutionEnv env) : base(env) {
            _extractProgramPath = Path.Combine(_tempDir, $"dbextract_{DateTime.Now:HHmmssfff}.p");
        }

        protected override string DatabaseExtractType => "all";

        protected override string DatabaseExtractExternalProgramPath => _extractProgramPath;

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            File.WriteAllText(_extractProgramPath, OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution_extract_db_schema.p"), Env.GetIoEncoding());
        }


        private string _extractProgramPath;

        protected override void ReadExtractionResults() {
            if (!string.IsNullOrEmpty(_databaseExtractFilePath) && File.Exists(_databaseExtractFilePath)) {

                using (var reader = new UoeExportReader(_databaseExtractFilePath, Env.GetIoEncoding())) {
                    string currentDatabaseName = null;
                    while (reader.ReadRecord(out List<string> fields, out int _, true)) {
                        if (fields.Count < 2) {
                            continue;
                        }
                        switch (fields[0]) {
                            case "D":
                                currentDatabaseName = fields[1];
                                break;
                            case "M":

                                break;
                        }
                    }
                }
            }
        }

    }
}
