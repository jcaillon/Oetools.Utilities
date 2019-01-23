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
        /// Creates a new database and loads the given schema definition file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="dfFilePath"></param>
        /// <param name="stFilePath"></param>
        /// <param name="blockSize"></param>
        /// <param name="codePage"></param>
        /// <param name="newInstance"></param>
        /// <param name="relativePath"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void CreateWithDf(UoeDatabaseLocation targetDb, string dfFilePath, string stFilePath = null, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true) {
            if (string.IsNullOrEmpty(stFilePath) && !string.IsNullOrEmpty(dfFilePath)) {
                // generate a structure file from df?
                stFilePath = GenerateStructureFileFromDf(targetDb, dfFilePath);
            }

            Create(targetDb, stFilePath, blockSize, codePage, newInstance, relativePath);

            // Load .df
            if (!string.IsNullOrEmpty(dfFilePath)) {
                LoadSchemaDefinition(UoeConnectionString.NewSingleUserConnection(targetDb), dfFilePath);
            }
        }

        /// <summary>
        /// Load a .df in a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dfFilePath">Path to the .df file to load.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadSchemaDefinition(UoeConnectionString connectionString, string dfFilePath) {
            dfFilePath = dfFilePath?.MakePathAbsolute();
            if (!File.Exists(dfFilePath)) {
                throw new UoeDatabaseException($"The schema definition file does not exist: {dfFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading schema definition file {dfFilePath.PrettyQuote()} in {connectionString.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"load-df|{dfFilePath}".CliQuoter()}");
        }

        /// <summary>
        /// Dump a .df from a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dfDumpFilePath">Path to the .df file to write.</param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpSchemaDefinition(UoeConnectionString connectionString, string dfDumpFilePath, string tableName = "ALL") {
            if (string.IsNullOrEmpty(dfDumpFilePath)) {
                throw new UoeDatabaseException("The definition file path can't be null.");
            }

            dfDumpFilePath = dfDumpFilePath.MakePathAbsolute();
            var dir = Path.GetDirectoryName(dfDumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Log?.Info($"Dumping schema definition to file {dfDumpFilePath.PrettyQuote()} from {connectionString.ToString().PrettyQuote()}.");

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
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpIncrementalSchemaDefinitionFromDatabases(IEnumerable<UoeConnectionString> connectionString, string incDfDumpFilePath, string renameFilePath = null) {
            if (!string.IsNullOrEmpty(renameFilePath)) {
                Log?.Info($"Using rename file {renameFilePath.PrettyQuote()}.");
            }

            incDfDumpFilePath = incDfDumpFilePath.MakePathAbsolute();
            var dir = Path.GetDirectoryName(incDfDumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var csList = connectionString.ToList();
            if (csList.Count != 2) {
                throw new UoeDatabaseException($"There should be exactly 2 databases specified in the connection string: {UoeConnectionString.GetConnectionString(csList).PrettyQuote()}.");
            }

            Log?.Info($"Dumping incremental schema definition to file {incDfDumpFilePath.PrettyQuote()} from {(csList[0].DatabaseLocation.Exists() ? csList[0].DatabaseLocation.FullPath : csList[0].DatabaseLocation.PhysicalName)} (old) and {(csList[1].DatabaseLocation.Exists() ? csList[1].DatabaseLocation.FullPath : csList[1].DatabaseLocation.PhysicalName)} (new).");

            StartDataAdministratorProgram($"{UoeConnectionString.GetConnectionString(csList)} -param {$"dump-inc|{incDfDumpFilePath}|{renameFilePath ?? ""}".CliQuoter()}");
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
                DumpIncrementalSchemaDefinitionFromDatabases(new List<UoeConnectionString> { UoeConnectionString.NewSingleUserConnection(newDb), UoeConnectionString.NewSingleUserConnection(previousDb)}, incDfDumpFilePath, renameFilePath);
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
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpSequenceData(UoeConnectionString connectionString, string dumpFilePath) {
            if (string.IsNullOrEmpty(dumpFilePath)) {
                throw new UoeDatabaseException("The sequence data file path can't be null.");
            }

            dumpFilePath = dumpFilePath.MakePathAbsolute();
            var dir = Path.GetDirectoryName(dumpFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Log?.Info($"Dumping sequence data to file {dumpFilePath.PrettyQuote()} from {connectionString.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"dump-seq|{dumpFilePath}".CliQuoter()}");
        }

        /// <summary>
        /// Load the value of each sequence of a database.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="sequenceDataFilePath">Path to the sequence data file to read.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadSequenceData(UoeConnectionString connectionString, string sequenceDataFilePath) {
            sequenceDataFilePath = sequenceDataFilePath?.MakePathAbsolute();
            if (!File.Exists(sequenceDataFilePath)) {
                throw new UoeDatabaseException($"The sequence data file does not exist: {sequenceDataFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading sequence data from file {sequenceDataFilePath.PrettyQuote()} to {connectionString.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"load-seq|{sequenceDataFilePath}".CliQuoter()}");
        }

        /// <summary>
        /// Dump database data in .d file (plain text). Each table data is written in the corresponding "table.d" file.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpData(UoeConnectionString connectionString, string dumpDirectoryPath, string tableName = "ALL") {
            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.MakePathAbsolute();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping data to directory {dumpDirectoryPath.PrettyQuote()} from {connectionString.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"dump-d|{dumpDirectoryPath}|{tableName}".CliQuoter()}");
        }

        /// <summary>
        /// Load database data from .d files (plain text). Each table data is read from the corresponding "table.d" file.
        /// </summary>
        /// <param name="connectionString">The connection string to the database.</param>
        /// <param name="dataDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadData(UoeConnectionString connectionString, string dataDirectoryPath, string tableName = "ALL") {
            dataDirectoryPath = dataDirectoryPath?.MakePathAbsolute();

            if (!Directory.Exists(dataDirectoryPath)) {
                throw new UoeDatabaseException($"The data directory does not exist: {dataDirectoryPath.PrettyQuote()}.");
            }

            Log?.Info($"Loading data from directory {dataDirectoryPath.PrettyQuote()} to {connectionString.ToString().PrettyQuote()}.");

            StartDataAdministratorProgram($"{connectionString} -param {$"load-d|{dataDirectoryPath}|{tableName}".CliQuoter()}");
        }


        private void StartDataAdministratorProgram(string parameters, string workingDirectory = null) {
            Progres.WorkingDirectory = workingDirectory ?? TempFolder;
            var arguments = $"-p {ProcedurePath.CliQuoter()} {parameters}";

            Log?.Debug($"Executing command:\n{$"{Progres.ExecutablePath?.CliQuoter()} {arguments}".CliCompactWhitespaces()}");
            var executionOk = Progres.TryExecute(arguments);

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
                throw new UoeDatabaseException(Progres.BatchOutput.ToString());
            }

            if (output.Length > 4) {
                Log?.Warn($"Warning messages published during the process:\n{output.Substring(0, output.Length - 4)}");
            } else {
                Log?.Debug($"Command output:\n{batchModeOutput}");
            }
        }
    }
}
