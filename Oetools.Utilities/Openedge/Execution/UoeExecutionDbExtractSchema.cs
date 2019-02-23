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
using System.Linq;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Database.Interfaces;
using Oetools.Utilities.Openedge.Execution.Exceptions;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {
    /// <summary>
    /// Get the datetime of the last update of databases schema.
    /// </summary>
    public class UoeExecutionDbExtractSchema : UoeExecutionDbExtract {

        /// <summary>
        /// List of databases dumped.
        /// </summary>
        /// <returns></returns>
        public List<IUoeDatabase> GetDatabases() => _extractedDatabases;

        public UoeExecutionDbExtractSchema(AUoeExecutionEnv env) : base(env) {
            _extractProgramPath = Path.Combine(_tempDir, $"dbextract_{DateTime.Now:HHmmssfff}.p");
        }

        private List<IUoeDatabase> _extractedDatabases;


        protected override string DatabaseExtractType => "external_program";

        protected override string DatabaseExtractExternalProgramPath => _extractProgramPath;

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            File.WriteAllText(_extractProgramPath, OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution_extract_db_schema.p"), Env.GetIoEncoding());
        }

        private string _extractProgramPath;

        /// <summary>
        /// Type factory for database objects. Override to read the dump file into custom types.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        protected virtual T GetNew<T>() where T : class {
            if (typeof(T) == typeof(IUoeDatabaseField)) {
                return new UoeDatabaseField() as T;
            }
            if (typeof(T) == typeof(IUoeDatabaseTable)) {
                return new UoeDatabaseTable() as T;
            }
            if (typeof(T) == typeof(IUoeDatabaseTrigger)) {
                return new UoeDatabaseTrigger() as T;
            }
            if (typeof(T) == typeof(IUoeDatabaseIndex)) {
                return new UoeDatabaseIndex() as T;
            }
            if (typeof(T) == typeof(IUoeDatabaseIndexField)) {
                return new UoeDatabaseIndexField() as T;
            }
            if (typeof(T) == typeof(IUoeDatabaseSequence)) {
                return new UoeDatabaseSequence() as T;
            }
            if (typeof(T) == typeof(IUoeDatabase)) {
                return new UoeDatabase() as T;
            }
            throw new ArgumentOutOfRangeException($"Unknown type: {typeof(T)}.");
        }

        protected override void ReadExtractionResults() {
            if (!string.IsNullOrEmpty(_databaseExtractFilePath) && File.Exists(_databaseExtractFilePath)) {
                _extractedDatabases = ReadDatabasesFromDumpFile(_databaseExtractFilePath);
            }
        }

        public List<IUoeDatabase> ReadDatabasesFromDumpFile(string filePath) {
            var output = new List<IUoeDatabase>();
            IUoeDatabase currentDatabase = null;
            IUoeDatabaseTable currentTable = null;
            using (var reader = new UoeExportReader(filePath, Env.GetIoEncoding())) {
                reader.QuestionMarkReturnsNull = true;
                while (reader.ReadRecord(out List<string> fields, out int _, true)) {
                    if (fields.Count < 2) {
                        continue;
                    }

                    switch (fields[0]) {
                        case "D":
                            currentDatabase = GetNew<IUoeDatabase>();
                            currentDatabase.ExtractionTime = DateTime.ParseExact(fields[1], "yyyyMMdd-HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                            currentDatabase.LogicalName = fields[2];
                            currentDatabase.PhysicalName = Path.GetFileName(fields[3]);
                            currentDatabase.Version = UoeUtilities.GetDatabaseVersionFromInternalVersion(int.Parse(fields[4]), int.Parse(fields[5]), out DatabaseBlockSize bs);
                            currentDatabase.BlockSize = bs;
                            currentDatabase.Charset = fields[6];
                            currentDatabase.Collation = fields[7];
                            output.Add(currentDatabase);
                            break;
                        case "S":
                            if (currentDatabase == null) {
                                throw new UoeExecutionException($"Found sequence without a database ({fields[1]}).");
                            }
                            var sequence = GetNew<IUoeDatabaseSequence>();
                            sequence.Name = fields[1];
                            sequence.CycleOnLimit = fields[2][0] == '1';
                            sequence.Increment = int.Parse(fields[3]);
                            sequence.Initial = int.Parse(fields[4]);
                            if (int.TryParse(fields[5], out int min)) {
                                sequence.Min = min;
                            }
                            if (int.TryParse(fields[6], out int max)) {
                                sequence.Max = max;
                            }
                            (currentDatabase.Sequences ?? (currentDatabase.Sequences = new List<IUoeDatabaseSequence>())).Add(sequence);
                            break;
                        case "T":
                            if (currentDatabase == null) {
                                throw new UoeExecutionException($"Found table without a database ({fields[1]}).");
                            }
                            currentTable = GetNew<IUoeDatabaseTable>();
                            currentTable.Name = fields[1];
                            currentTable.DumpName = fields[2];
                            currentTable.Crc = ushort.Parse(fields[3]);
                            currentTable.Label = fields[4];
                            currentTable.LabelAttribute = fields[5];
                            currentTable.Description = fields[6];
                            currentTable.Hidden = fields[7][0] == '1';
                            currentTable.Frozen = fields[8][0] == '1';
                            currentTable.Area = fields[9];
                            if (Enum.TryParse(fields[10], true, out UoeDatabaseTableType type)) {
                                currentTable.Type = type;
                            }
                            currentTable.ValidationExpression = fields[11];
                            currentTable.ValidationMessage = fields[12];
                            currentTable.ValidationMessageAttribute = fields[13];
                            currentTable.Replication = fields[14];
                            currentTable.Foreign = fields[15];
                            (currentDatabase.Tables ?? (currentDatabase.Tables = new List<IUoeDatabaseTable>())).Add(currentTable);
                            break;
                        case "F":
                            if (currentTable == null) {
                                throw new UoeExecutionException($"Found field without a table ({fields[1]}).");
                            }
                            var field = GetNew<IUoeDatabaseField>();
                            field.Name = fields[1];
                            if (Enum.TryParse(fields[2].Replace("-", ""), true, out UoeDatabaseDataType dataType)) {
                                field.DataType = dataType;
                            }
                            field.Format = fields[3];
                            field.FormatAttribute = fields[4];
                            field.Order = int.Parse(fields[5]);
                            field.Position = int.Parse(fields[6]);
                            field.Mandatory = fields[7][0] == '1';
                            field.CaseSensitive = fields[8][0] == '1';
                            field.Extent = int.Parse(fields[9]);
                            field.InitialValue = fields[10];
                            field.InitialValueAttribute = fields[11];
                            field.Width = int.Parse(fields[12]);
                            field.Label = fields[13];
                            field.LabelAttribute = fields[14];
                            field.ColumnLabel = fields[15];
                            field.ColumnLabelAttribute = fields[16];
                            field.Description = fields[17];
                            field.Help = fields[18];
                            field.HelpAttribute = fields[19];
                            if (int.TryParse(fields[20], out int decimals)) {
                                field.Decimals = decimals;
                            }
                            if (field.DataType == UoeDatabaseDataType.Clob) {
                                field.ClobCharset = fields[21];
                                field.ClobCollation = fields[22];
                                field.ClobType = int.Parse(fields[23]);
                            }
                            if (field.DataType == UoeDatabaseDataType.Blob || field.DataType == UoeDatabaseDataType.Clob) {
                                field.LobSize = fields[24];
                                if (int.TryParse(fields[25], out int lobBytes)) {
                                    field.LobBytes = lobBytes;
                                }
                                field.LobArea = fields[26];
                            }
                            (currentTable.Fields ?? (currentTable.Fields = new List<IUoeDatabaseField>())).Add(field);
                            break;
                        case "X":
                            if (currentTable == null) {
                                throw new UoeExecutionException($"Found trigger without a table ({fields[1]}).");
                            }
                            var trigger = GetNew<IUoeDatabaseTrigger>();
                            if (Enum.TryParse(fields[1].Replace("-", ""), true, out UoeDatabaseTriggerEvent triggerEvent)) {
                                trigger.Event = triggerEvent;
                            }
                            trigger.Procedure = fields[3];
                            trigger.Overridable = fields[4][0] == '1';
                            if (ushort.TryParse(fields[5], out ushort crc)) {
                                trigger.Crc = crc;
                            }
                            if (!string.IsNullOrEmpty(fields[2])) {
                                var triggerField = currentTable.Fields?.FirstOrDefault(f => f.Name.Equals(fields[2], StringComparison.CurrentCultureIgnoreCase));
                                if (triggerField == null) {
                                    throw new UoeExecutionException($"Found field trigger without a corresponding field ({fields[2]}).");
                                }
                                (triggerField.Triggers ?? (triggerField.Triggers = new List<IUoeDatabaseTrigger>())).Add(trigger);
                            } else {
                                (currentTable.Triggers ?? (currentTable.Triggers = new List<IUoeDatabaseTrigger>())).Add(trigger);
                            }
                            break;
                        case "I":
                            if (currentTable == null) {
                                throw new UoeExecutionException($"Found index without a table ({fields[1]}).");
                            }
                            var index = GetNew<IUoeDatabaseIndex>();
                            index.Name = fields[1];
                            index.Active = fields[2][0] == '1';
                            index.Primary = fields[3][0] == '1';
                            index.Unique = fields[4][0] == '1';
                            index.Word = fields[5][0] == '1';
                            index.Crc = ushort.Parse(fields[6]);
                            index.Area = fields[7];
                            var indexFieldStrings = fields[8].Split(',');
                            index.Description = fields[9];
                            foreach (var indexFieldString in indexFieldStrings) {
                                var indexFieldFound = currentTable.Fields?.FirstOrDefault(f => f.Name.Equals(indexFieldString.Substring(2), StringComparison.CurrentCultureIgnoreCase));
                                if (indexFieldFound == null) {
                                    throw new UoeExecutionException($"Found index without a corresponding field ({indexFieldString.Substring(2)}).");
                                }
                                var indexField = GetNew<IUoeDatabaseIndexField>();
                                indexField.Ascending = indexFieldString[0] == '+';
                                indexField.Abbreviate = indexFieldString[1] == '1';
                                indexField.Field = indexFieldFound;
                                (index.Fields ?? (index.Fields = new List<IUoeDatabaseIndexField>())).Add(indexField);
                            }
                            (currentTable.Indexes ?? (currentTable.Indexes = new List<IUoeDatabaseIndex>())).Add(index);
                            break;
                    }
                }
            }

            return output;
        }
    }
}
