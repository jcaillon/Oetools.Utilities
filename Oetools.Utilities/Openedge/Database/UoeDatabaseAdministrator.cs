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
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Database {

    public class UoeDatabaseAdministrator : UoeDatabaseOperator, IDisposable {

        private UoeProcessIo _progres;

        private string _procedurePath;
        private string _tempFolder;

        private string ProcedurePath {
            get {
                if (_procedurePath == null) {
                    _procedurePath = Path.Combine(TempFolder, $"db_admin_{Path.GetRandomFileName()}.p");
                    File.WriteAllText(ProcedurePath, OpenedgeResources.GetOpenedgeAsStringFromResources(@"database_administrator.p"), Encoding);
                }
                return _procedurePath;
            }
        }

        private UoeProcessIo Progres {
            get {
                if (_progres == null) {
                    _progres = new UoeProcessIo(DlcPath, true);
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
                LoadSchemaDefinition(targetDbPath, dfFilePath);
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
            LoadSchemaDefinition(targetDbPath, dfFilePath);
        }

        /// <summary>
        /// Load a .df in a database
        /// </summary>
        /// <param name="targetDbPath">Path to the target database.</param>
        /// <param name="dfFilePath">Path to the .df file to load.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void LoadSchemaDefinition(string targetDbPath, string dfFilePath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string _, true);

            if (string.IsNullOrEmpty(dfFilePath)) {
                throw new UoeDatabaseOperationException("The schema definition file path can't be null.");
            }

            dfFilePath = dfFilePath.MakePathAbsolute();

            if (!File.Exists(dfFilePath)) {
                throw new UoeDatabaseOperationException($"The schema definition file does not exist: {dfFilePath.PrettyQuote()}.");
            }

            var getBusyMode = GetBusyMode(targetDbPath);
            if (getBusyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseOperationException("The database is currently busy in single user mode, kick the user or start the database for multi-user mode.");
            }

            var connectionString = AddMaxConnectionTry(getBusyMode == DatabaseBusyMode.NotBusy ? GetSingleUserConnectionString(targetDbPath, "DICTDB") : GetMultiUserConnectionString(targetDbPath, logicalName: "DICTDB"));

            Progres.WorkingDirectory = dbFolder;

            Log?.Info($"Loading schema definition file {dfFilePath.PrettyQuote()} in {targetDbPath.PrettyQuote()}.");

            var executionOk = Progres.TryExecute($"{connectionString} -p {ProcedurePath.CliQuoter()} -param {$"load-df|{dfFilePath}".CliQuoter()}");
            var output = Progres.BatchOutput.ToString();
            if (!executionOk || !output.EndsWith("OK")) {
                throw new UoeDatabaseOperationException(Progres.BatchOutput.ToString());
            }
            if (output.Length > 4) {
                Log?.Warn($"Message when loading the definition schema:\n{output.Substring(0, output.Length - 4)}");
            }
        }

        /// <summary>
        /// Dump a .df from a database.
        /// </summary>
        /// <param name="targetDbPath">Path to the target database.</param>
        /// <param name="dfFilePath">Path to the .df file to write.</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpSchemaDefinition(string targetDbPath, string dfFilePath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string _, true);

            if (string.IsNullOrEmpty(dfFilePath)) {
                throw new UoeDatabaseOperationException("The definition file path can't be null.");
            }

            dfFilePath = dfFilePath.MakePathAbsolute();

            var dir = Path.GetDirectoryName(dfFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            var getBusyMode = GetBusyMode(targetDbPath);
            if (getBusyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseOperationException("The database is currently busy in single user mode, kick the user or start the database for multi-user mode.");
            }

            var connectionString = AddMaxConnectionTry(getBusyMode == DatabaseBusyMode.NotBusy ? GetSingleUserConnectionString(targetDbPath, "DICTDB") : GetMultiUserConnectionString(targetDbPath, logicalName: "DICTDB"));

            Progres.WorkingDirectory = dbFolder;

            Log?.Info($"Dumping schema definition to file {dfFilePath.PrettyQuote()} from {targetDbPath.PrettyQuote()}.");

            var executionOk = Progres.TryExecute($"{connectionString} -p {ProcedurePath.CliQuoter()} -param {$"dump-df|{dfFilePath}".CliQuoter()}");
            var output = Progres.BatchOutput.ToString();
            if (!executionOk || !output.EndsWith("OK")) {
                throw new UoeDatabaseOperationException(Progres.BatchOutput.ToString());
            }
            if (output.Length > 4) {
                Log?.Warn($"Message when dumping the definition schema:\n{output.Substring(0, output.Length - 4)}");
            }
        }

        /// <inheritdoc cref="DumpIncrementalSchemaDefinition"/>
        /// <summary>
        /// Dumps an incremental schema definition file with the difference between two databases.
        /// </summary>
        /// <param name="beforeDbPath"></param>
        /// <param name="afterDbPath"></param>
        /// <param name="incDfPath"></param>
        /// <param name="renameFilePath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpIncrementalSchemaDefinitionFromDatabases(string beforeDbPath, string afterDbPath, string incDfPath, string renameFilePath = null) {
            GetDatabaseFolderAndName(beforeDbPath, out string _, out string _, true);
            GetDatabaseFolderAndName(afterDbPath, out string _, out string _, true);

            var getBusyMode = GetBusyMode(beforeDbPath);
            if (getBusyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseOperationException("The previous database is currently busy in single user mode, kick the user or start the database for multi-user mode.");
            }
            var connectionString = AddMaxConnectionTry(getBusyMode == DatabaseBusyMode.NotBusy ? GetSingleUserConnectionString(beforeDbPath, "before") : GetMultiUserConnectionString(beforeDbPath, logicalName: "before"));


            getBusyMode = GetBusyMode(afterDbPath);
            if (getBusyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseOperationException("The new database is currently busy in single user mode, kick the user or start the database for multi-user mode.");
            }
            connectionString += $" {AddMaxConnectionTry(getBusyMode == DatabaseBusyMode.NotBusy ? GetSingleUserConnectionString(afterDbPath, "after") : GetMultiUserConnectionString(afterDbPath, logicalName: "after"))}";

            var dir = Path.GetDirectoryName(incDfPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            Progres.WorkingDirectory = TempFolder;

            Log?.Info($"Dumping incremental schema definition to file {incDfPath.PrettyQuote()} from {beforeDbPath.PrettyQuote()} (before) and {afterDbPath.PrettyQuote()} (after).");

            var executionOk = Progres.TryExecute($"{connectionString} -p {ProcedurePath.CliQuoter()} -param {$"dump-inc|{incDfPath}|{renameFilePath ?? ""}".CliQuoter()}");
            var output = Progres.BatchOutput.ToString();
            if (!executionOk || !output.EndsWith("OK")) {
                throw new UoeDatabaseOperationException(Progres.BatchOutput.ToString());
            }
            if (output.Length > 4) {
                Log?.Warn($"Message when dumping the incremental definition schema:\n{output.Substring(0, output.Length - 4)}");
            }
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
        /// <param name="incDfPath"></param>
        /// <param name="renameFilePath"></param>
        public void DumpIncrementalSchemaDefinition(string beforeDfPath, string afterDfPath, string incDfPath, string renameFilePath = null) {
            var tempFolder = Path.Combine(TempFolder, Path.GetRandomFileName());
            Directory.CreateDirectory(tempFolder);
            try {
                var previousDbPath = Path.Combine(tempFolder, "dbprev.db");
                var newDbPath = Path.Combine(tempFolder, "dbnew.db");
                CreateCompilationDatabaseFromDf(previousDbPath, beforeDfPath);
                CreateCompilationDatabaseFromDf(newDbPath, afterDfPath);
                DumpIncrementalSchemaDefinitionFromDatabases(previousDbPath, newDbPath, incDfPath, renameFilePath);
            } finally {
                Directory.Delete(tempFolder, true);
            }
        }
    }
}
