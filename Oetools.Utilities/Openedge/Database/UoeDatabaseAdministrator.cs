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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Database.Exceptions;
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

        private string SqlSchemaName => Utils.IsRuntimeWindowsPlatform ? "_sqlschema.exe" : "_sqlschema";
        private string SqlLoadName => Utils.IsRuntimeWindowsPlatform ? "_sqlload.exe" : "_sqlload";
        private string SqlDumpName => Utils.IsRuntimeWindowsPlatform ? "_sqldump.exe" : "_sqldump";

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
                        RedirectedOutputEncoding = Encoding,
                        Log = Log
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
        /// Pro parameters to append to the execution of the progress process.
        /// </summary>
        public string ProExeCommandLineParameters { get; set; }

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
        /// Creates a new database and loads the given schema definition file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="dfFilePath"></param>
        /// <param name="stFilePath"></param>
        /// <param name="blockSize"></param>
        /// <param name="codePage"></param>
        /// <param name="newInstance">Specifies that a new GUID be created for the target database.</param>
        /// <param name="relativePath">Use relative path in the structure file.</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void CreateWithDf(UoeDatabaseLocation targetDb, string dfFilePath, string stFilePath = null, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true) {
            if (string.IsNullOrEmpty(stFilePath) && !string.IsNullOrEmpty(dfFilePath)) {
                // generate a structure file from df?
                stFilePath = GenerateStructureFileFromDf(targetDb, dfFilePath);
            }

            Create(targetDb, stFilePath, blockSize, codePage, newInstance, relativePath);

            // Load .df
            if (!string.IsNullOrEmpty(dfFilePath)) {
                LoadSchemaDefinition(UoeDatabaseConnection.NewSingleUserConnection(targetDb), dfFilePath);
            }
        }

        /// <summary>
        /// Load a .df in a database.
        /// </summary>
        /// <param name="databaseConnection">The connection string to the database.</param>
        /// <param name="dfFilePath">Path to the .df file to load.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadSchemaDefinition(UoeDatabaseConnection databaseConnection, string dfFilePath) {
            dfFilePath = dfFilePath?.ToAbsolutePath();
            if (!File.Exists(dfFilePath)) {
                throw new UoeDatabaseException($"The schema definition file does not exist: {dfFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading schema definition file {dfFilePath.PrettyQuote()} in {databaseConnection.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{databaseConnection} -param {$"load-df|{dfFilePath}".Quoter()}");
        }

        /// <summary>
        /// Dump a .df from a database.
        /// </summary>
        /// <param name="databaseConnection">The connection string to the database.</param>
        /// <param name="dfDumpFilePath">Path to the .df file to write.</param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpSchemaDefinition(UoeDatabaseConnection databaseConnection, string dfDumpFilePath, string tableName = "ALL") {
            if (string.IsNullOrEmpty(dfDumpFilePath)) {
                throw new UoeDatabaseException("The definition file path can't be null.");
            }

            dfDumpFilePath = dfDumpFilePath.ToAbsolutePath();
            var dir = Path.GetDirectoryName(dfDumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Log?.Info($"Dumping schema definition to file {dfDumpFilePath.PrettyQuote()} from {databaseConnection.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{databaseConnection} -param {$"dump-df|{dfDumpFilePath}".Quoter()}|{tableName}");
        }

        /// <inheritdoc cref="DumpIncrementalSchemaDefinition"/>
        /// <summary>
        /// Dumps an incremental schema definition file with the difference between two databases.
        /// The first database should be the database "after" and second "before".
        /// </summary>
        /// <remarks>
        /// renameFilePath : It is a plain text file used to identify database tables and fields that have changed names. This allows to avoid having a DROP then ADD table when you               /// changed only the name of said table.
        /// The format of the file is simple (comma separated lines, don't forget to add a final empty line for IMPORT):
        /// - T,old-table-name,new-table-name
        /// - F,table-name,old-field-name,new-field-name
        /// - S,old-sequence-name,new-sequence-name
        /// Missing entries or entries with an empty new name are considered to have been deleted.
        /// </remarks>
        /// <param name="databaseConnections">The connection string to the database.</param>
        /// <param name="incDfDumpFilePath"></param>
        /// <param name="renameFilePath">It is a plain text file used to identify database tables and fields that have changed names.</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpIncrementalSchemaDefinitionFromDatabases(IEnumerable<UoeDatabaseConnection> databaseConnections, string incDfDumpFilePath, string renameFilePath = null) {
            if (!string.IsNullOrEmpty(renameFilePath)) {
                Log?.Info($"Using rename file {renameFilePath.PrettyQuote()}.");
            }

            incDfDumpFilePath = incDfDumpFilePath.ToAbsolutePath();
            var dir = Path.GetDirectoryName(incDfDumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var csList = databaseConnections.ToList();
            if (csList.Count != 2) {
                throw new UoeDatabaseException($"There should be exactly 2 databases specified in the connection string: {UoeDatabaseConnection.GetConnectionString(csList).PrettyQuote()}.");
            }

            Log?.Info($"Dumping incremental schema definition to file {incDfDumpFilePath.PrettyQuote()} from {(csList[0].DatabaseLocation.Exists() ? csList[0].DatabaseLocation.FullPath : csList[0].DatabaseLocation.PhysicalName)} (old) and {(csList[1].DatabaseLocation.Exists() ? csList[1].DatabaseLocation.FullPath : csList[1].DatabaseLocation.PhysicalName)} (new).");

            StartDataAdministratorProgram($"{UoeDatabaseConnection.GetConnectionString(csList, true)} -param {$"dump-inc|{incDfDumpFilePath}|{renameFilePath ?? ""}".Quoter()}");
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
                var previousDb = new UoeDatabaseLocation(Path.Combine(tempFolder, "dbprev.db"));
                var newDb = new UoeDatabaseLocation(Path.Combine(tempFolder, "dbnew.db"));
                CreateWithDf(previousDb, beforeDfPath);
                CreateWithDf(newDb, afterDfPath);
                DumpIncrementalSchemaDefinitionFromDatabases(new List<UoeDatabaseConnection> { UoeDatabaseConnection.NewSingleUserConnection(newDb), UoeDatabaseConnection.NewSingleUserConnection(previousDb)}, incDfDumpFilePath, renameFilePath);
            } finally {
                Directory.Delete(tempFolder, true);
            }
        }

        /// <summary>
        /// Dump the value of each sequence of a database.
        /// </summary>
        /// <param name="databaseConnection">The connection string to the database.</param>
        /// <param name="dumpFilePath">Path to the sequence data file to write.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpSequenceData(UoeDatabaseConnection databaseConnection, string dumpFilePath) {
            if (string.IsNullOrEmpty(dumpFilePath)) {
                throw new UoeDatabaseException("The sequence data file path can't be null.");
            }

            dumpFilePath = dumpFilePath.ToAbsolutePath();
            var dir = Path.GetDirectoryName(dumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Log?.Info($"Dumping sequence data to file {dumpFilePath.PrettyQuote()} from {databaseConnection.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{databaseConnection} -param {$"dump-seq|{dumpFilePath}".Quoter()}");
        }

        /// <summary>
        /// Load the value of each sequence of a database.
        /// </summary>
        /// <param name="databaseConnection">The connection string to the database.</param>
        /// <param name="sequenceDataFilePath">Path to the sequence data file to read.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadSequenceData(UoeDatabaseConnection databaseConnection, string sequenceDataFilePath) {
            sequenceDataFilePath = sequenceDataFilePath?.ToAbsolutePath();
            if (!File.Exists(sequenceDataFilePath)) {
                throw new UoeDatabaseException($"The sequence data file does not exist: {sequenceDataFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading sequence data from file {sequenceDataFilePath.PrettyQuote()} to {databaseConnection.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{databaseConnection} -param {$"load-seq|{sequenceDataFilePath}".Quoter()}");
        }

        /// <summary>
        /// Dump database data in .d file (plain text). Each table data is written in the corresponding "table.d" file.
        /// </summary>
        /// <param name="databaseConnection">The connection string to the database.</param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpData(UoeDatabaseConnection databaseConnection, string dumpDirectoryPath, string tableName = "ALL") {
            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.ToAbsolutePath();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping data to directory {dumpDirectoryPath.PrettyQuote()} from {databaseConnection.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{databaseConnection} -param {$"dump-d|{dumpDirectoryPath}|{tableName}".Quoter()}");
        }

        /// <summary>
        /// Load database data from .d files (plain text). Each table data is read from the corresponding "table.d" file.
        /// </summary>
        /// <param name="databaseConnection">The connection string to the database.</param>
        /// <param name="dataDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadData(UoeDatabaseConnection databaseConnection, string dataDirectoryPath, string tableName = "ALL") {
            dataDirectoryPath = dataDirectoryPath?.ToAbsolutePath();

            if (!Directory.Exists(dataDirectoryPath)) {
                throw new UoeDatabaseException($"The data directory does not exist: {dataDirectoryPath.PrettyQuote()}.");
            }

            Log?.Info($"Loading data from directory {dataDirectoryPath.PrettyQuote()} to {databaseConnection.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{databaseConnection} -param {$"load-d|{dataDirectoryPath}|{tableName}".Quoter()}");
        }

        /// <summary>
        /// Dump sql database definition. SQL-92 format.
        /// </summary>
        /// <param name="databaseConnection"></param>
        /// <param name="dumpFilePath"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpSqlSchema(UoeDatabaseConnection databaseConnection, string dumpFilePath, string options = "-f %.% -g %.% -G %.% -n %.% -p %.% -q %.% -Q %.% -s %.% -t %.% -T %.%") {
            dumpFilePath = dumpFilePath.ToAbsolutePath();
            var dir = Path.GetDirectoryName(dumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var sqlSchema = GetExecutable(SqlSchemaName);
            sqlSchema.WorkingDirectory = TempFolder;

            Log?.Info($"Dump sql-92 definition from {databaseConnection.ToString().PrettyQuote()} to {dumpFilePath.PrettyQuote()}.");

            TryExecuteWithJdbcConnection(sqlSchema, $"-u {databaseConnection.UserId.Quoter()} -a {databaseConnection.Password.Quoter()} -o {dumpFilePath.Quoter()} {options}", databaseConnection);
            if (sqlSchema.BatchOutputString.Length > 0) {
                throw new UoeDatabaseException(sqlSchema.BatchOutputString);
            }
        }

        /// <summary>
        /// Dump data in SQL-92 format.
        /// </summary>
        /// <param name="databaseConnection"></param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpSqlData(UoeDatabaseConnection databaseConnection, string dumpDirectoryPath, string options = "-t %.%") {
            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.ToAbsolutePath();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping sql-92 data to directory {dumpDirectoryPath.PrettyQuote()} from {databaseConnection.ToString().PrettyQuote()}.");

            var sqlDump = GetExecutable(SqlDumpName);
            sqlDump.WorkingDirectory = dumpDirectoryPath;

            TryExecuteWithJdbcConnection(sqlDump, $"-u {databaseConnection.UserId.Quoter()} -a {databaseConnection.Password.Quoter()} {options}", databaseConnection);
            if (sqlDump.ExitCode != 0) {
                throw new UoeDatabaseException(sqlDump.BatchOutputString);
            }

            var output = sqlDump.ErrorOutput.ToString();
            if (output.Length > 0) {
                Log?.Warn(output);
            }
            output = sqlDump.StandardOutput.ToString();
            if (output.Length > 0) {
                Log?.Info(output);
            }
        }

        /// <summary>
        /// Load data in SQL-92 format.
        /// </summary>
        /// <param name="databaseConnection"></param>
        /// <param name="dataDirectoryPath"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadSqlData(UoeDatabaseConnection databaseConnection, string dataDirectoryPath, string options = "-t %.%") {
            dataDirectoryPath = dataDirectoryPath?.ToAbsolutePath();

            if (!Directory.Exists(dataDirectoryPath)) {
                throw new UoeDatabaseException($"The data directory does not exist: {dataDirectoryPath.PrettyQuote()}.");
            }

            Log?.Info($"Loading sql-92 data from directory {dataDirectoryPath.PrettyQuote()} to {databaseConnection.ToString().PrettyQuote()}.");

            var sqlLoad = GetExecutable(SqlLoadName);
            sqlLoad.WorkingDirectory = dataDirectoryPath;

            TryExecuteWithJdbcConnection(sqlLoad, $"-u {databaseConnection.UserId.Quoter()} -a {databaseConnection.Password.Quoter()} {options}", databaseConnection);
            if (sqlLoad.ExitCode != 0) {
                throw new UoeDatabaseException(sqlLoad.BatchOutputString);
            }

            var output = sqlLoad.ErrorOutput.ToString();
            if (output.Length > 0) {
                Log?.Warn(output);
            }
            output = sqlLoad.StandardOutput.ToString();
            if (output.Length > 0) {
                Log?.Info(output);
            }
        }

        private bool TryExecuteWithJdbcConnection(ProcessIo process, string arguments, UoeDatabaseConnection databaseConnection) {
            bool executionOk;
            var busyMode = GetBusyMode(databaseConnection.DatabaseLocation);
            if (string.IsNullOrEmpty(databaseConnection.Service) && busyMode == DatabaseBusyMode.NotBusy) {
                Log?.Debug("The database needs to be started for this operation, starting it.");
                using (var db = new UoeDatabaseStarted(this, databaseConnection.DatabaseLocation)) {
                    db.AllowsDatabaseShutdownWithKill = false;
                    executionOk = process.TryExecute($"{arguments} {db.GetDatabaseConnection().ToCliArgsJdbcConnectionString(false)}");
                }
            } else if (string.IsNullOrEmpty(databaseConnection.Service)) {
                throw new UoeDatabaseException("The database needs to be either not busy or started for multi-user mode with service port.");
            } else {
                executionOk = process.TryExecute($"{arguments} {databaseConnection.ToCliArgsJdbcConnectionString(false)}");
            }
            return executionOk;
        }

        private void StartDataAdministratorProgram(string parameters, string workingDirectory = null) {
            var arguments = $"-p {ProcedurePath.Quoter()} {parameters}{(!string.IsNullOrEmpty(workingDirectory) ? $" -T {TempFolder.Quoter()}" : "")}";
            if (!string.IsNullOrWhiteSpace(ProExeCommandLineParameters)) {
                arguments = $"{arguments} {ProExeCommandLineParameters}";
            }

            Progres.WorkingDirectory = workingDirectory ?? TempFolder;
            var executionOk = Progres.TryExecute(arguments);

            if (!executionOk || !Progres.BatchOutputString.EndsWith("OK")) {
                throw new UoeDatabaseException(Progres.BatchOutputString);
            }

            if (Progres.BatchOutputString.Length > 4) {
                Log?.Warn($"Warning messages published during the process:\n{Progres.BatchOutputString.Substring(0, Progres.BatchOutputString.Length - 4)}");
            }
        }
    }
}
