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
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// Get the the crc of all the tables and a list of sequences.
    /// </summary>
    public class UoeExecutionDbExtractTableCrcAndSequenceList : UoeExecutionDbExtract {

        public UoeExecutionDbExtractTableCrcAndSequenceList(AUoeExecutionEnv env) : base(env) { }

        public override string DatabaseExtractCandoTblType { get; set; } = "T,S";

        public override string DatabaseExtractCandoTblName { get; set; } = "_Sequence,!_*,*";

        protected override string DatabaseExtractType => "crc";

        /// <summary>
        /// Get a list with all the tables and their CRC value
        /// (key, value) = (qualified table name, CRC)
        /// qualified table name = DATABASE_NAME.TABLE_NAME
        /// Note : if aliases exists, the alias versions will be listed as well ALIAS_NAME.TABLE_NAME
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> TablesCrc => _tablesCrc ?? new Dictionary<string, string>();

        /// <summary>
        /// Get a list of all the sequences (with qualified name, i.e. DATABASE_NAME.SEQUENCE_NAME)
        /// Note : if aliases exists, the alias versions will be listed as well ALIAS_NAME.SEQUENCE_NAME
        /// </summary>
        /// <returns></returns>
        public HashSet<string> Sequences => _sequences ?? new HashSet<string>();

        private Dictionary<string, string> _tablesCrc;

        private HashSet<string> _sequences;

        protected override void ReadExtractionResults() {
            if (!string.IsNullOrEmpty(_databaseExtractFilePath) && File.Exists(_databaseExtractFilePath)) {
                _tablesCrc = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                _sequences = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
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
                            case "S":
                                if (string.IsNullOrEmpty(currentDatabaseName)) {
                                    continue;
                                }
                                var dbNames = new List<string>{currentDatabaseName};
                                if (Env.DatabaseAliases != null) {
                                    dbNames.AddRange(Env.DatabaseAliases.Where(a => a.DatabaseLogicalName.EqualsCi(currentDatabaseName)).Select(a => a.AliasLogicalName));
                                }
                                foreach (var db in dbNames) {
                                    var key = $"{db}.{fields[1]}";
                                    if (!_sequences.Contains(key)) {
                                        _sequences.Add(key);
                                    }
                                }
                                break;
                            case "T":
                                if (string.IsNullOrEmpty(currentDatabaseName) || fields.Count < 3) {
                                    continue;
                                }
                                var dbNames2 = new List<string>{currentDatabaseName};
                                if (Env.DatabaseAliases != null) {
                                    dbNames2.AddRange(Env.DatabaseAliases.Where(a => a.DatabaseLogicalName.EqualsCi(currentDatabaseName)).Select(a => a.AliasLogicalName));
                                }
                                foreach (var db in dbNames2) {
                                    var key = $"{db}.{fields[1]}";
                                    if (!_tablesCrc.ContainsKey(key)) {
                                        _tablesCrc.Add(key, fields[2]);
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
