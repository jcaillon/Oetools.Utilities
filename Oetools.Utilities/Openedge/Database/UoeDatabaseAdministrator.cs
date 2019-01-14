#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseAdministrator.cs) is part of Oetools.Utilities.
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
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Administrate an openedge database.
    /// </summary>
    public class UoeDatabaseAdministrator : UoeDatabaseOperator, IDisposable {

        private UoeProcessIo _progres;

        private string _procedurePath;
        private string _tempFolder;

        private string ProcedurePath {
            get {
                if (_procedurePath == null) {
                    _procedurePath = Path.Combine(TempFolder, $"db_admin_{Path.GetRandomFileName()}.p");
                    File.WriteAllText(ProcedurePath, OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_database_administrator.p"), Encoding);
                }
                return _procedurePath;
            }
        }

        private UoeProcessIo Progres {
            get {
                if (_progres == null) {
                    _progres = new UoeProcessIo(DlcPath, true) {
                        CancelToken = CancelToken,
                        RedirectedOutputEncoding = Encoding
                    };
                }
                return _progres;
            }
        }

        /// <summary>
        /// The temp folder to use when we need to write the openedge procedure for data administration
        /// </summary>
        public string TempFolder {
            get => _tempFolder ?? (_tempFolder = Utils.CreateTempDirectory());
            set => _tempFolder = value;
        }

        /// <summary>
        /// Initialize a new instance.
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="encoding"></param>
        public UoeDatabaseAdministrator(string dlcPath, Encoding encoding = null) : base(dlcPath, encoding) {

        }

        public void Dispose() {
            _progres?.Dispose();
            _progres = null;
            if (!string.IsNullOrEmpty(_procedurePath)) {
                File.Delete(_procedurePath);
            }
        }

        /// <summary>
        /// Creates a new database.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="stFilePath"></param>
        /// <param name="blockSize"></param>
        /// <param name="codePage"></param>
        /// <param name="newInstance"></param>
        /// <param name="relativePath"></param>
        /// <param name="dfFilePath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void CreateDatabase(string targetDbPath, string stFilePath = null, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true, string dfFilePath = null) {
            // exists?
            if (DatabaseExists(targetDbPath)) {
                throw new UoeDatabaseOperationException($"The target database already exists, choose a new name or delete the existing database: {targetDbPath.PrettyQuote()}.");
            }

            if (!string.IsNullOrEmpty(stFilePath)) {
                CopyStructureFile(targetDbPath, stFilePath);
            } else if (!string.IsNullOrEmpty(dfFilePath)) {
                // generate a structure file from df?
                stFilePath = GenerateStructureFileFromDf(targetDbPath, dfFilePath);
            }

            if (!string.IsNullOrEmpty(stFilePath)) {
                ProstrctCreate(targetDbPath, stFilePath, blockSize);
            }

            Procopy(targetDbPath, blockSize, codePage, newInstance, relativePath);

            // Load .df
            if (!string.IsNullOrEmpty(dfFilePath)) {
                LoadSchemaDefinition(GetSingleUserConnectionString(targetDbPath), dfFilePath);
            }
        }

        /// <summary>
        /// Generates a database from a .df (database definition) file.
        /// That database should not be used in production since it has all default configuration, its purpose is to exist for file compilation.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="dfFilePath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void CreateCompilationDatabaseFromDf(string targetDbPath, string dfFilePath) {
            ProstrctCreate(targetDbPath, GenerateStructureFileFromDf(targetDbPath, dfFilePath));
            Procopy(targetDbPath);
            LoadSchemaDefinition(GetSingleUserConnectionString(targetDbPath), dfFilePath);
        }

        /// <summary>
        /// Load a .df in a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dfFilePath">Path to the .df file to load.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void LoadSchemaDefinition(string connectionString, string dfFilePath) {
            connectionString = MakeDbPathAbsolute(connectionString);

            dfFilePath = dfFilePath?.MakePathAbsolute();
            if (!File.Exists(dfFilePath)) {
                throw new UoeDatabaseOperationException($"The schema definition file does not exist: {dfFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading schema definition file {dfFilePath.PrettyQuote()} in {connectionString.PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"load-df|{dfFilePath}".CliQuoter()}");
        }

        /// <summary>
        /// Dump a .df from a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dfDumpFilePath">Path to the .df file to write.</param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpSchemaDefinition(string connectionString, string dfDumpFilePath, string tableName = "ALL") {
            connectionString = MakeDbPathAbsolute(connectionString);

            if (string.IsNullOrEmpty(dfDumpFilePath)) {
                throw new UoeDatabaseOperationException("The definition file path can't be null.");
            }

            dfDumpFilePath = dfDumpFilePath.MakePathAbsolute();
            var dir = Path.GetDirectoryName(dfDumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Log?.Info($"Dumping schema definition to file {dfDumpFilePath.PrettyQuote()} from {connectionString.PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"dump-df|{dfDumpFilePath}".CliQuoter()}|{tableName}");
        }

        /// <inheritdoc cref="DumpIncrementalSchemaDefinition"/>
        /// <summary>
        /// Dumps an incremental schema definition file with the difference between two databases.
        /// The first database should be the database "after" and second "before".
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="incDfDumpFilePath"></param>
        /// <param name="renameFilePath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpIncrementalSchemaDefinitionFromDatabases(string connectionString, string incDfDumpFilePath, string renameFilePath = null) {
            connectionString = MakeDbPathAbsolute(connectionString);

            if (!string.IsNullOrEmpty(renameFilePath)) {
                Log?.Info($"Using rename file {renameFilePath.PrettyQuote()}.");
            }

            incDfDumpFilePath = incDfDumpFilePath.MakePathAbsolute();
            var dir = Path.GetDirectoryName(incDfDumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var matches = new Regex(@"-db\s+(""(?<quotedpath>[^""]+)""|(?<path>[^\s]+))").Matches(connectionString);

            if (matches.Count != 2) {
                throw new UoeDatabaseOperationException($"There should be exactly 2 databases specified in the connection string: {connectionString.PrettyQuote()}.");
            }

            Log?.Info($"Dumping incremental schema definition to file {incDfDumpFilePath.PrettyQuote()} from {(matches[1].Groups["path"].Success ? matches[1].Groups["path"].Success : matches[1].Groups["quotedpath"].Success)} (old) and {(matches[0].Groups["path"].Success ? matches[0].Groups["path"].Success : matches[0].Groups["quotedpath"].Success)} (new).");

            StartDataAdministratorProgram($"{connectionString} -param {$"dump-inc|{incDfDumpFilePath}|{renameFilePath ?? ""}".CliQuoter()}");
        }

        /// <summary>
        /// Dumps an incremental schema definition file with the difference between two schema definition files.
        /// </summary>
        /// <remarks>
        /// The rename-file parameter is used to identify tables, database fields and sequences that have changed names.
        /// The format of the file is a comma separated list that identifies the renamed object, its old name and the new name.
        /// Missing entries or entries with new name empty or null are considered deleted.
        ///  The rename-file has following format:
        ///  T,old-table-name,new-table-name
        ///  F,table-name,old-field-name,new-field-name
        ///  S,old-sequence-name,new-sequence-name
        /// </remarks>
        /// <param name="beforeDfPath"></param>
        /// <param name="afterDfPath"></param>
        /// <param name="incDfDumpFilePath"></param>
        /// <param name="renameFilePath"></param>
        public void DumpIncrementalSchemaDefinition(string beforeDfPath, string afterDfPath, string incDfDumpFilePath, string renameFilePath = null) {
            var tempFolder = Path.Combine(TempFolder, Path.GetRandomFileName());
            Directory.CreateDirectory(tempFolder);
            try {
                var previousDbPath = Path.Combine(tempFolder, "dbprev.db");
                var newDbPath = Path.Combine(tempFolder, "dbnew.db");
                CreateCompilationDatabaseFromDf(previousDbPath, beforeDfPath);
                CreateCompilationDatabaseFromDf(newDbPath, afterDfPath);
                DumpIncrementalSchemaDefinitionFromDatabases($"{GetConnectionString(newDbPath)} {GetConnectionString(previousDbPath)}", incDfDumpFilePath, renameFilePath);
            } finally {
                Directory.Delete(tempFolder, true);
            }
        }

        /// <summary>
        /// Dump the value of each sequence of a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dumpFilePath">Path to the sequence data file to write.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpSequenceData(string connectionString, string dumpFilePath) {
            connectionString = MakeDbPathAbsolute(connectionString);

            if (string.IsNullOrEmpty(dumpFilePath)) {
                throw new UoeDatabaseOperationException("The sequence data file path can't be null.");
            }

            dumpFilePath = dumpFilePath.MakePathAbsolute();
            var dir = Path.GetDirectoryName(dumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Log?.Info($"Dumping sequence data to file {dumpFilePath.PrettyQuote()} from {connectionString.PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"dump-seq|{dumpFilePath}".CliQuoter()}");
        }

        /// <summary>
        /// Load the value of each sequence of a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="sequenceDataFilePath">Path to the sequence data file to read.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void LoadSequenceData(string connectionString, string sequenceDataFilePath) {
            connectionString = MakeDbPathAbsolute(connectionString);

            sequenceDataFilePath = sequenceDataFilePath?.MakePathAbsolute();
            if (!File.Exists(sequenceDataFilePath)) {
                throw new UoeDatabaseOperationException($"The sequence data file does not exist: {sequenceDataFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading sequence data from file {sequenceDataFilePath.PrettyQuote()} to {connectionString.PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"load-seq|{sequenceDataFilePath}".CliQuoter()}");
        }

        /// <summary>
        /// Dump database data in .d file (plain text). Each table data is written in the corresponding "table.d" file.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpData(string connectionString, string dumpDirectoryPath, string tableName = "ALL") {
            connectionString = MakeDbPathAbsolute(connectionString);

            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseOperationException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.MakePathAbsolute();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping data to directory {dumpDirectoryPath.PrettyQuote()} from {connectionString.PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"dump-d|{dumpDirectoryPath}|{tableName}".CliQuoter()}");
        }

        /// <summary>
        /// Load database data from .d files (plain text). Each table data is read from the corresponding "table.d" file.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dataDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void LoadData(string connectionString, string dataDirectoryPath, string tableName = "ALL") {
            connectionString = MakeDbPathAbsolute(connectionString);

            dataDirectoryPath = dataDirectoryPath?.MakePathAbsolute();

            if (!Directory.Exists(dataDirectoryPath)) {
                throw new UoeDatabaseOperationException($"The data directory does not exist: {dataDirectoryPath.PrettyQuote()}.");
            }

            Log?.Info($"Loading data from directory {dataDirectoryPath.PrettyQuote()} to {connectionString.PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"load-d|{dataDirectoryPath}|{tableName}".CliQuoter()}");
        }

        public string MakeDbPathAbsolute(string connectionString, string currentDirectory = null) {
            return new Regex(@"-db\s+(""(?<quotedpath>[^""]+)""|(?<path>[^\s]+))").Replace(connectionString, me => {
                if (me.Groups["quotedpath"].Success) {
                    return $"-db \"{me.Groups["quotedpath"].Value.MakePathAbsolute(currentDirectory)}\"";
                }
                return $"-db \"{me.Groups["path"].Value.MakePathAbsolute()}\"";
            });
        }

        private void StartDataAdministratorProgram(string parameters, string workingDirectory = null) {
            Progres.WorkingDirectory = workingDirectory ?? TempFolder;
            var executionOk = Progres.TryExecute($"-p {ProcedurePath.CliQuoter()} {parameters}");
            var batchModeOutput = new StringBuilder();
            foreach (var s in Progres.ErrorOutputArray.ToNonNullEnumerable()) {
                batchModeOutput.AppendLine(s.Trim());
            }
            batchModeOutput.TrimEnd();
            foreach (var s in Progres.StandardOutputArray.ToNonNullEnumerable()) {
                batchModeOutput.AppendLine(s.Trim());
            }
            batchModeOutput.TrimEnd();
            var output = batchModeOutput.ToString();
            if (!executionOk || !output.EndsWith("OK")) {
                throw new UoeDatabaseOperationException(Progres.BatchOutput.ToString());
            }
            if (output.Length > 4) {
                Log?.Warn($"Warning messages published during the process:\n{output.Substring(0, output.Length - 4)}");
            }
        }
    }
}
