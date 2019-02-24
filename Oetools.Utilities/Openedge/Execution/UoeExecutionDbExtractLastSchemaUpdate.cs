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
using System.Globalization;
using System.IO;
using System.Text;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// Get the datetime of the last update of databases schema.
    /// </summary>
    public class UoeExecutionDbExtractLastSchemaUpdate : AUoeExecutionDbExtract {

        public UoeExecutionDbExtractLastSchemaUpdate(AUoeExecutionEnv env) : base(env) { }

        protected override string DatabaseExtractType => "last_schema_update";

        /// <summary>
        /// Get a list with all the connected databases and their datetime of last schema update.
        /// (key, value) = (database name, last schema update)
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, DateTime> LastSchemaUpdates => _lastSchemaUpdates ?? new Dictionary<string, DateTime>();

        private Dictionary<string, DateTime> _lastSchemaUpdates;

        protected override void ReadExtractionResults() {
            if (!string.IsNullOrEmpty(_databaseExtractFilePath) && File.Exists(_databaseExtractFilePath)) {
                _lastSchemaUpdates = new Dictionary<string, DateTime>(StringComparer.CurrentCultureIgnoreCase);
                using (var reader = new UoeExportReader(_databaseExtractFilePath, Env.IoEncoding)) {
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
                                if (string.IsNullOrEmpty(currentDatabaseName)) {
                                    continue;
                                }
                                if (!_lastSchemaUpdates.ContainsKey(currentDatabaseName)) {
                                    // Wed Aug 16 17:56:59 2017
                                    if (DateTime.TryParseExact(fields[1], "ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out DateTime updateDate)) {
                                        _lastSchemaUpdates.Add(currentDatabaseName, updateDate);

                                    }
                                }
                                break;
                        }
                    }
                }
            }
        }
    }
}
