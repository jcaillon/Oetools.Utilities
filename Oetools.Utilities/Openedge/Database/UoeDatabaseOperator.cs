#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseOperator.cs) is part of Oetools.Utilities.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Allows to interact with an openedge database at a file system level : create/start/shutdown and so on...
    /// TODO: -C bulkload
    /// </summary>
    public class UoeDatabaseOperator {

        /// <summary>
        /// Path to the openedge installation folder
        /// </summary>
        protected string DlcPath { get; }

        /// <summary>
        /// Returns the output value of the last operation done
        /// </summary>
        public string LastOperationOutput => _lastUsedProcess.BatchOutputString;

        private ProcessIoWithLog _lastUsedProcess;

        private Dictionary<string, ProcessIoWithLog> _processIos = new Dictionary<string, ProcessIoWithLog>();

        /// <summary>
        /// Returns the path to _dbutil (or null if not found in the dlc folder)
        /// </summary>
        private string DbUtilName => Utils.IsRuntimeWindowsPlatform ? "_dbutil.exe" : "_dbutil";

        /// <summary>
        /// Returns the path to _proutil (or null if not found in the dlc folder)
        /// </summary>
        private string ProUtilName => Utils.IsRuntimeWindowsPlatform ? "_proutil.exe" : "_proutil";

        /// <summary>
        /// Returns the path to _mprosrv (or null if not found in the dlc folder)
        /// </summary>
        private string ProservePath => Path.Combine(DlcPath, "bin", Utils.IsRuntimeWindowsPlatform ? "_mprosrv.exe" : "_mprosrv");

        /// <summary>
        /// Returns the path to _mprshut (or null if not found in the dlc folder)
        /// </summary>
        private string ProshutName => Utils.IsRuntimeWindowsPlatform ? "_mprshut.exe" : "_mprshut";

        /// <summary>
        /// Internationalization startup parameters such as -cpinternal codepage and -cpstream codepage.
        /// They will be used for commands that support them.
        /// </summary>
        public string InternationalizationStartupParameters { get; set; }

        /// <summary>
        /// Database access/encryption parameters:
        /// [[-userid username [-password passwd ]] | [ -U username -P passwd] ]
        /// [-Passphrase]
        /// </summary>
        public string DatabaseAccessStartupParameters { get; set; }

        /// <summary>
        /// The encoding to use for I/O of the openedge executables.
        /// </summary>
        public Encoding Encoding { get; } = Encoding.Default;

        /// <summary>
        /// A logger.
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        public CancellationToken? CancelToken { get; set; }

        /// <summary>
        /// New database utility.
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="encoding"></param>
        /// <exception cref="ArgumentException"></exception>
        public UoeDatabaseOperator(string dlcPath, Encoding encoding = null) {
            if (encoding != null) {
                Encoding = encoding;
            }
            DlcPath = dlcPath;
            if (string.IsNullOrEmpty(dlcPath) || !Directory.Exists(dlcPath)) {
                throw new ArgumentException($"Invalid dlc path {dlcPath.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Backup a database. The database can either be started (online backup) or stopped (offline backup).
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="targetBackupFile"></param>
        /// <param name="verbose"></param>
        /// <param name="scan"></param>
        /// <param name="compressed"></param>
        /// <param name="extra"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Backup(UoeDatabase targetDb, string targetBackupFile, bool verbose = true, bool scan = true, bool compressed = true, string extra = null) {
            targetDb.ThrowIfNotExist();

            var busyMode = GetBusyMode(targetDb);
            if (busyMode == DatabaseBusyMode.SingleUser) {
                throw new ArgumentException($"The database is used in single user mode: {targetDb.FullPath.PrettyQuote()}.");
            }

            extra = extra?.Replace("online", "") ?? "";
            if (busyMode == DatabaseBusyMode.MultiUser) {
                // can't use -scan in online mode
                extra = extra.Replace("-scan", "");
            } else if (scan) {
                extra += " -scan";
            }
            if (compressed) {
                extra += " -com";
            }
            if (verbose) {
                extra += " -verbose";
            }

            targetBackupFile = targetBackupFile.MakePathAbsolute();
            var dir = Path.GetDirectoryName(targetBackupFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            if (GetBusyMode(targetDb) == DatabaseBusyMode.NotBusy) {
                Log?.Debug($"Backing up database {targetDb.FullPath.PrettyQuote()} in offline mode to file {targetBackupFile.PrettyQuote()}.");

                var dbUtil = GetExecutable(DbUtilName);
                dbUtil.WorkingDirectory = targetDb.DirectoryPath;

                var executionOk = dbUtil.TryExecute($"probkup {targetDb.PhysicalName} {targetBackupFile.CliQuoter()} {extra} {InternationalizationStartupParameters}");
                if (!executionOk || !dbUtil.BatchOutputString.Contains("(3740)")) {
                    // Backup complete. (3740)
                    throw new UoeDatabaseException(dbUtil.BatchOutputString);
                }
            } else {
                Log?.Debug($"Backing up database {targetDb.FullPath.PrettyQuote()} in online mode to file {targetBackupFile.PrettyQuote()}.");

                var proShut = GetExecutable(ProshutName);
                proShut.WorkingDirectory = targetDb.DirectoryPath;

                var executionOk = proShut.TryExecute($"{targetDb.PhysicalName} -C backup online {targetBackupFile.CliQuoter()} {extra} {InternationalizationStartupParameters}");
                if (!executionOk || !proShut.BatchOutputString.Contains("(3740)")) {
                    // Backup complete. (3740)
                    throw new UoeDatabaseException(proShut.BatchOutputString);
                }
            }

        }

        /// <summary>
        /// Truncates the log file. If the database is started, it will re-log the database startup parameters to the log file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void TruncateLog(UoeDatabase targetDb) {
            targetDb.ThrowIfNotExist();

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            string online =  null;
            if (GetBusyMode(targetDb) == DatabaseBusyMode.MultiUser) {
                online = " -online";
                Log?.Debug("The database is served for multi-users, using the `-online` option.");
            }

            try {
                dbUtil.Execute($"prolog {targetDb.PhysicalName}{online} {InternationalizationStartupParameters}");
            } catch(Exception) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Dump binary data for the given table.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="options"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpBinaryData(UoeDatabase targetDb, string tableName, string dumpDirectoryPath, string options = null) {
            targetDb.ThrowIfNotExist();

            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.MakePathAbsolute();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping binary data of table {tableName} to directory {dumpDirectoryPath.PrettyQuote()} from {targetDb.FullPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute($"{targetDb.PhysicalName} -C dump {tableName} {dumpDirectoryPath.CliQuoter()} {options} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk || !proUtil.BatchOutputString.Contains("(6254)")) {
                // Binary Dump complete. (6254)
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Load binary data to the given database.
        /// The index of the loaded tables should be rebuilt afterward.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="options"></param>
        /// <param name="binDataFilePath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadBinaryData(UoeDatabase targetDb, string binDataFilePath, string options = null) {
            targetDb.ThrowIfNotExist();

            binDataFilePath = binDataFilePath?.MakePathAbsolute();
            if (!File.Exists(binDataFilePath)) {
                throw new UoeDatabaseException($"The binary data file does not exist: {binDataFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading binary data from {binDataFilePath.PrettyQuote()} to {targetDb.FullPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute($"{targetDb.PhysicalName} -C load {binDataFilePath.CliQuoter()} {options} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk || !proUtil.BatchOutputString.Contains("(6256)")) {
                // Binary Load complete. (6256)
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Rebuild the indexed of a database. By default, rebuilds all the active indexes.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void RebuildIndexes(UoeDatabase targetDb, string options = "activeindexes") {
            targetDb.ThrowIfNotExist();

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute($"{targetDb.PhysicalName} -C idxbuild {options} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk || !proUtil.BatchOutputString.Contains("(11465)")|| !proUtil.BatchOutputString.Contains(" 0 err")) {
                // 1 indexes were rebuilt. (11465)
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Creates a void OpenEdge database from a previously defined structure description (.st) file.
        /// You will need to a schema to this void database by using <see cref="ProcopyEmpty"/>.
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        internal void CreateVoidDatabase(UoeDatabase targetDb, string structureFilePath, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform) {
            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            structureFilePath = structureFilePath?.MakePathAbsolute();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            CreateExtentsDirectories(UoeDatabase.FromOtherFilePath(structureFilePath));

            if (!Directory.Exists(targetDb.DirectoryPath)) {
                Directory.CreateDirectory(targetDb.DirectoryPath);
            }

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Creating database structure for {targetDb.FullPath.PrettyQuote()} from {structureFilePath.PrettyQuote()} with block size {blockSize.ToString().Substring(1)}.");

            var executionOk = dbUtil.TryExecute($"prostrct create {targetDb.PhysicalName} {structureFilePath.CliQuoter()} -blocksize {blockSize.ToString().Substring(1)} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Validates whether or not the structure file is correct. Return null if correct, the errors otherwise.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        internal string ValidateStructureFile(string structureFilePath) {
            structureFilePath = structureFilePath?.MakePathAbsolute();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            var dbUtil = GetExecutable(DbUtilName);

            Log?.Info($"Validating structure file {structureFilePath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"prostrct create dummy {structureFilePath.CliQuoter()} -validate {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk) {
                return dbUtil.BatchOutputString;
            }
            return null;
        }

        /// <summary>
        /// Creates/updates a structure description (.st) file from the .db file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void UpdateStructureFile(UoeDatabase targetDb) {
            targetDb.ThrowIfNotExist();

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            var structureFilePath = Path.Combine(targetDb.DirectoryPath, $"{targetDb.PhysicalName}.st");
            Log?.Info($"{(File.Exists(structureFilePath) ? "Updating" : "Creating")} structure file (.st) for the database {targetDb.FullPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"prostrct list {targetDb.PhysicalName} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Creates the structure file (.st) from the .db file if it does not exist (or does nothing).
        /// </summary>
        /// <param name="targetDb"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        private void CreateStructureFileIfNeeded(UoeDatabase targetDb) {
            targetDb.ThrowIfNotExist();

            if (!File.Exists(targetDb.StructureFileFullPath)) {
                Log?.Debug($"The structure file does not exist, creating it: {targetDb.StructureFileFullPath.PrettyQuote()}.");
                UpdateStructureFile(targetDb);
            }
        }

        /// <summary>
        /// Updates a database's control information (.db file) after an extent is moved or renamed.
        /// Does a prostrct repair or a prostrct builddb depending wether or not the .db exists.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void UpdateDatabaseControlInfo(UoeDatabase targetDb) {
            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            if (!File.Exists(targetDb.StructureFileFullPath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {targetDb.StructureFileFullPath.PrettyQuote()}.");
            }

            if (targetDb.Exists()) {
                Log?.Info($"Repairing database control information (.db) of {targetDb.FullPath.PrettyQuote()} from {targetDb.StructureFileFullPath}.");

                var executionOk = dbUtil.TryExecute($"prostrct repair {targetDb.PhysicalName} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
                if (!executionOk || !dbUtil.BatchOutputString.Contains("(13485)")) {
                    // repair of *** ended. (13485)
                    throw new UoeDatabaseException(dbUtil.BatchOutputString);
                }
            } else {
                Log?.Info($"Creating database control information (.db) of {targetDb.FullPath.PrettyQuote()} from {targetDb.StructureFileFullPath}.");

                var executionOk = dbUtil.TryExecute($"prostrct builddb {targetDb.PhysicalName} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
                if (!executionOk) {
                    throw new UoeDatabaseException(dbUtil.BatchOutputString);
                }
            }
        }

        /// <summary>
        /// Appends the files from a new structure description (.st) file to a database.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="structureFilePath"></param>
        /// <param name="validate"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void AddStructureDefinition(UoeDatabase targetDb, string structureFilePath, bool validate = false) {
            targetDb.ThrowIfNotExist();

            structureFilePath = structureFilePath?.MakePathAbsolute();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            string qualifier = "add";
            if (GetBusyMode(targetDb) == DatabaseBusyMode.MultiUser) {
                qualifier = "addonline";
                Log?.Debug($"The database is served for multi-users, using the `{qualifier}` qualifier.");
            }

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Appending new extents from {structureFilePath.PrettyQuote()} to the database structure of {targetDb.FullPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"prostrct {qualifier} {targetDb.PhysicalName} {structureFilePath.CliQuoter()} {(validate ? "-validate" : "")} {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            if (!executionOk || dbUtil.BatchOutputString.Contains("(12867)")) {
                // prostrct add FAILED. (12867)
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }

            // we updated .db, now update the .st with the .db
            UpdateStructureFile(targetDb);
        }

        /// <summary>
        /// Copy a database (procopy).
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="sourceDb">Path of the procopy source database</param>
        /// <param name="newInstance">Use -newinstance in procopy command</param>
        /// <param name="relativePath">Use -relativepath in procopy command</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Copy(UoeDatabase targetDb, UoeDatabase sourceDb, bool newInstance = true, bool relativePath = true) {
            sourceDb.ThrowIfNotExist();

            if (!Directory.Exists(targetDb.DirectoryPath)) {
                Directory.CreateDirectory(targetDb.DirectoryPath);
            }

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            CreateExtentsDirectories(targetDb);

            Log?.Info($"Copying database {sourceDb.FullPath.PrettyQuote()} to {targetDb.FullPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"procopy {sourceDb.FullPath.CliQuoter()} {targetDb.PhysicalName}{(newInstance ? " -newinstance" : "")}{(relativePath ? " -relative" : "")} {InternationalizationStartupParameters}");
            if (!executionOk || !dbUtil.BatchOutputString.Contains("(1365)")) {
                // db copied from C:\progress\client\v117x_dv\dlc\empty1. (1365)
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Copy an empty database from the openedge installation directory.
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="codePage">Database codepage (copy from $DLC/prolang/codepage/emptyX)</param>
        /// <param name="newInstance">Use -newinstance in procopy command</param>
        /// <param name="relativePath">Use -relativepath in procopy command</param>
        /// <exception cref="UoeDatabaseException"></exception>
        internal void ProcopyEmpty(UoeDatabase targetDb, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true) {
            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            string emptyDirectoryPath;
            if (!string.IsNullOrEmpty(codePage)) {
                emptyDirectoryPath = Path.Combine(DlcPath, "prolang", codePage);
                if (!Directory.Exists(emptyDirectoryPath)) {
                    throw new UoeDatabaseException($"Invalid codepage, the folder doesn't exist: {emptyDirectoryPath.PrettyQuote()}.");
                }
            } else {
                emptyDirectoryPath = DlcPath;
            }

            var sourceDb = new UoeDatabase(Path.Combine(emptyDirectoryPath, $"empty{(int) blockSize}.db"));

            if (!sourceDb.Exists()) {
                throw new UoeDatabaseException($"Could not find the procopy source database: {sourceDb.FullPath.PrettyQuote()}.");
            }

            Copy(targetDb, sourceDb, newInstance, relativePath);
        }

        /// <summary>
        /// Create a new empty database.
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="codePage">Database codepage (copy from $DLC/prolang/codepage/emptyX)</param>
        /// <param name="newInstance">Use -newinstance in procopy command</param>
        /// <param name="relativePath">Use -relativepath in procopy command</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Create(UoeDatabase targetDb, string structureFilePath = null, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true) {
            if (targetDb.Exists()) {
                throw new UoeDatabaseException($"The database already exists: {targetDb.FullPath.PrettyQuote()}.");
            }

            if (!string.IsNullOrEmpty(structureFilePath)) {
                CreateVoidDatabase(targetDb, structureFilePath, blockSize);
            }
            ProcopyEmpty(targetDb, blockSize, codePage, newInstance, relativePath);
        }

        /// <summary>
        /// Start a databaser server
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <returns>start parameters string</returns>
        public string ProServe(UoeDatabase targetDb, int? nbUsers = null, string options = null) {
            return ProServe(targetDb, null, nbUsers, options);
        }

        /// <summary>
        /// Start a databaser server
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="servicePort"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <returns>start parameters string</returns>
        public string ProServe(UoeDatabase targetDb, int servicePort, int? nbUsers = null, string options = null) {
            return ProServe(targetDb, servicePort.ToString(), nbUsers, options);
        }

        /// <summary>
        /// Start a database server
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="serviceName"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        /// <returns>start parameters string</returns>
        public string ProServe(UoeDatabase targetDb, string serviceName, int? nbUsers = null, string options = null) {
            return ProServe(targetDb, null, serviceName, nbUsers, options);
        }

        /// <summary>
        /// Start a database server
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="hostname"></param>
        /// <param name="serviceName"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        /// <returns>start parameters string</returns>
        public string ProServe(UoeDatabase targetDb, string hostname, string serviceName, int? nbUsers = null, string options = null) {
            targetDb.ThrowIfNotExist();

            if (nbUsers != null) {
                var mn = 1;
                var ma = nbUsers;
                var userOptions = $"-n {mn * ma + 1} -Mi {ma} -Ma {ma} -Mn {mn} -Mpb {mn}";
                options = options == null ? userOptions : $"{userOptions} {options}";
            }

            // check if busy
            DatabaseBusyMode busyMode = GetBusyMode(targetDb);
            if (busyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseException("Database already used in single user mode.");
            }
            if (busyMode == DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseException("Database already used in multi user mode.");
            }

            if (!string.IsNullOrEmpty(hostname)) {
                options = $"-N TCP -H {hostname} {options}";
            }

            if (!string.IsNullOrEmpty(serviceName)) {
                options = $"-S {serviceName} {options}";
            }

            options = $"{targetDb.PhysicalName} {options} {InternationalizationStartupParameters}".CliCompactWhitespaces();

            Log?.Info($"Starting database server for {targetDb.FullPath.PrettyQuote()}.");

            var proservePath = ProservePath;

            if (!File.Exists(proservePath)) {
                throw new UoeDatabaseException($"The proserve executable does not exist: {proservePath.PrettyQuote()}.");
            }

            Log?.Debug($"Executing command:\n{$"{proservePath.CliQuoter()} {options}".CliCompactWhitespaces()}");

            var proc = Process.Start(new ProcessStartInfo {
                FileName = proservePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = options,
                WorkingDirectory = targetDb.DirectoryPath
            });

            if (proc == null) {
                throw new UoeDatabaseException($"Failed to start {ProservePath.PrettyQuote()} with options: {options.PrettyQuote()}.");
            }

            do {
                busyMode = GetBusyMode(targetDb);
            } while (busyMode != DatabaseBusyMode.MultiUser && !proc.HasExited);

            if (busyMode != DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseException($"Failed to serve the database, check the database log file {Path.Combine(targetDb.DirectoryPath, $"{targetDb.PhysicalName}.lg").PrettyQuote()}, options used: {options.PrettyQuote()}.");
            }

            return options;
        }

        /// <summary>
        /// Returns the busy mode of the database, indicating if the database is used in single/multi user mode
        /// </summary>
        /// <param name="targetDb"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public DatabaseBusyMode GetBusyMode(UoeDatabase targetDb) {
            targetDb.ThrowIfNotExist();

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;
            proUtil.Log = null;
            try {
                proUtil.Execute($"{targetDb.PhysicalName} -C busy {InternationalizationStartupParameters} {DatabaseAccessStartupParameters}");
            } catch (Exception) {
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            } finally {
                proUtil.Log = Log;
            }

            var output = proUtil.BatchOutputString;
            switch (output) {
                case string _ when output.Contains("(276)"):
                    return DatabaseBusyMode.MultiUser;
                case string _ when output.Contains("(263)"):
                    return DatabaseBusyMode.SingleUser;
                default:
                    return DatabaseBusyMode.NotBusy;
            }
        }

        /// <summary>
        /// Reads information from a database log file.
        /// </summary>
        /// <param name="logFilePath"></param>
        /// <param name="hostName"></param>
        /// <param name="serviceName"></param>
        internal static void ReadLogFile(string logFilePath, out string hostName, out string serviceName) {
            // read the log file in reverse order, trying to get the hostname and service name used to start the database.
            hostName = null;
            serviceName = null;
            if (!File.Exists(logFilePath)) {
                return;
            }
            foreach (var line in new ReverseLineReader(logFilePath, Encoding.ASCII)) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                var idx = line.IndexOf('(');
                var proNumber = idx > 0 && line.Length > idx + 6 ? line.Substring(idx + 1, 4) : null;
                switch (proNumber) {
                    case "4261":
                        // BROKER  0: (4261)  Host Name (-H): localhost.
                        idx = line.IndexOf(':', idx + 6);
                        hostName = idx > 0 && line.Length > idx ? line.Substring(idx + 1).Trim().TrimEnd('.') : null;
                        // If not -H was specified when starting the db, the -H will equal to the current hostname.
                        // But you can't connect with this hostname so we correct it here.
                        if (!string.IsNullOrEmpty(hostName) && hostName.Equals(GetHostName(), StringComparison.OrdinalIgnoreCase)) {
                            hostName = "localhost";
                        }
                        return;
                    case "4262":
                        // BROKER  0: (4262)  Service Name (-S): 0.
                        idx = line.IndexOf(':', idx + 6);
                        serviceName = idx > 0 && line.Length > idx ? line.Substring(idx + 1).Trim().TrimEnd('.') : null;
                        continue;
                    default:
                        continue;
                }
            }
        }

        private static string GetHostName() {
            try {
                var hostname = Dns.GetHostName();
                Dns.GetHostEntry(hostname);
                return hostname;
            } catch (Exception) {
                return "localhost";
            }
        }

        /// <summary>
        /// Shutdown a database started in multi user mode
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Proshut(UoeDatabase targetDb, string options = null) {
            targetDb.ThrowIfNotExist();

            var proshut = GetExecutable(ProshutName);
            proshut.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Shutting down database server for {targetDb.FullPath.PrettyQuote()}.");

            proshut.TryExecute($"{targetDb.PhysicalName} -by {options} {InternationalizationStartupParameters}");

            if (GetBusyMode(targetDb) != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseException(proshut.BatchOutputString);
            }
        }

        /// <summary>
        /// Deletes the database, expects the database to be stopped first. Does not delete the .st file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Delete(UoeDatabase targetDb) {
            targetDb.ThrowIfNotExist();

            var busyMode = GetBusyMode(targetDb);
            if (busyMode != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseException($"The database is still in use: {busyMode}.");
            }

            if (!File.Exists(targetDb.StructureFileFullPath)) {
                Log?.Debug($"The structure file does not exist, creating it: {targetDb.StructureFileFullPath.PrettyQuote()}");
                UpdateStructureFile(targetDb);
            }

            Log?.Info($"Deleting database files for {targetDb.FullPath.PrettyQuote()} using the content of {targetDb.PhysicalName}.st.");

            foreach (var file in ListDatabaseFiles(targetDb)) {
                Log?.Debug($"Deleting: {file.PrettyQuote()}.");
                File.Delete(file);
            }

        }

        /// <summary>
        /// Creates the necessary directories to create the extents listed in the .st file.
        /// </summary>
        /// <param name="targetDb"></param>
        public void CreateExtentsDirectories(UoeDatabase targetDb) {
            CreateStructureFileIfNeeded(targetDb);
            if (!File.Exists(targetDb.StructureFileFullPath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {targetDb.StructureFileFullPath}.");
            }

            foreach (var file in ListDatabaseFiles(targetDb, false)) {
                var dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Log?.Debug($"Creating directory: {file.PrettyQuote()}.");
                    Directory.CreateDirectory(dir);
                }
            }
        }

        /// <summary>
        /// Get a list of files used by the database described by the given .st path.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="filesMustExist"></param>
        /// <returns></returns>
        public IEnumerable<String> ListDatabaseFiles(UoeDatabase targetDb, bool filesMustExist = true) {
            var stRegex = new Regex(@"^(?<type>[abdt])(?<areainfo>\s""(?<areaname>[\w\s]+)""(:(?<areanum>[0-9]+))?(,(?<recsPerBlock>[0-9]+))?(;(?<blksPerCluster>[0-9]+))?)?\s((?<path>[^\s""!]+)|!""(?<pathquoted>[^""]+)"")(\s(?<extentType>[f|v])\s(?<extentSize>[0-9]+))?", RegexOptions.Multiline);

            foreach (var ext in new List<string> { "lk", "lic", "lg", "db" }) {
                var path = Path.ChangeExtension(targetDb.FullPath, ext);
                if (!filesMustExist || File.Exists(path)) {
                    yield return path;
                }
            }

            if (string.IsNullOrEmpty(targetDb.FullPath) || !File.Exists(targetDb.FullPath)) {
                yield break;
            }

            var areas = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            var areaNumAuto = 6;
            foreach (Match match in stRegex.Matches(File.ReadAllText(targetDb.StructureFileFullPath))) {
                var directory = match.Groups["pathquoted"].Value;
                if (string.IsNullOrEmpty(directory)) {
                    directory = match.Groups["path"].Value;
                }

                if (string.IsNullOrEmpty(directory)) {
                    continue;
                }
                directory = directory.MakePathAbsolute(targetDb.DirectoryPath);

                var areaType = match.Groups["type"].Value;
                var isSchemaOrData = areaType == "d";
                var areaName = match.Groups["areaname"].Value;

                var areaId = $"{areaType}{areaName}{match.Groups["areanum"].Value}";
                if (!areas.ContainsKey(areaId)) {
                    areas.Add(areaId, 1);
                    if (isSchemaOrData) {
                        areaNumAuto++;
                    }
                } else {
                    areas[areaId]++;
                }

                var areaNum = match.Groups["areanum"].Success ? int.Parse(match.Groups["areanum"].Value) : areaNumAuto;
                if (areaName.Equals("Schema Area", StringComparison.CurrentCultureIgnoreCase)) {
                    areaNum = 6;
                }

                var suffix = isSchemaOrData && areaNum > 6 ? $"_{areaNum}" : "";
                var fileName = $"{targetDb.PhysicalName}{suffix}.{areaType}{areas[areaId]}";
                var filePath = directory.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ? directory : Path.Combine(directory, fileName);

                if (!filesMustExist || File.Exists(filePath)) {
                    yield return filePath;
                }
            }
        }

        /// <summary>
        /// Generates a .st file at the given location, create all the needed AREA found in the given .df
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="sourceDfPath"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        /// <returns>file path to the created structure file</returns>
        public string GenerateStructureFileFromDf2(UoeDatabase targetDb, string sourceDfPath) {
            sourceDfPath = sourceDfPath?.MakePathAbsolute();
            if (string.IsNullOrEmpty(sourceDfPath) || !File.Exists(sourceDfPath)) {
                throw new UoeDatabaseException($"The file path for data definition file .df does not exist: {sourceDfPath.PrettyQuote()}.");
            }

            var stContent = new StringBuilder("b .\n");
            stContent.Append("d \"Schema Area\" .\n");
            var areaAdded = new HashSet<string> { "Schema Area" };
            foreach (Match areaName in new Regex("AREA \"([^\"]+)\"").Matches(File.ReadAllText(sourceDfPath, Encoding))) {
                if (!areaAdded.Contains(areaName.Groups[1].Value)) {
                    stContent.Append($"d {areaName.Groups[1].Value.CliQuoter()} .\n");
                    areaAdded.Add(areaName.Groups[1].Value);
                }
            }

            try {
                File.WriteAllText(targetDb.StructureFileFullPath, stContent.ToString(), Encoding.ASCII);
            } catch (Exception e) {
                throw new UoeDatabaseException($"Could not write {targetDb.StructureFileFullPath.PrettyQuote()}: {e.Message}.", e);
            }

            Log?.Info($"Generated database physical structure file {targetDb.StructureFileFullPath.PrettyQuote()} from schema definition file {sourceDfPath.PrettyQuote()}.");

            return targetDb.StructureFileFullPath;
        }

        /// <summary>
        /// Copy a source .st file to the target database directory, replacing any specific path by the relative path "."
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="targetStFilePath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public string MakeStructureFileUseRelativePath(UoeDatabase targetDb, string targetStFilePath = null) {
            if (!File.Exists(targetDb.StructureFileFullPath)) {
                throw new UoeDatabaseException($"Could not find the source structure file: {targetDb.StructureFileFullPath.PrettyQuote()}.");
            }

            targetStFilePath = targetStFilePath?.MakePathAbsolute() ?? targetDb.StructureFileFullPath;

            var newContent = new Regex("^(?<firstpart>\\w\\s+(\"[^\"]+\"(:\\d+)?(,\\d+)?(;\\d+)?)?\\s+)(?<path>\\S+|\"[^\"]+\")(?<extendTypeSize>(\\s+\\w+\\s+\\d+)?\\s*)$", RegexOptions.Multiline)
                .Replace(File.ReadAllText(targetStFilePath, Encoding), match => {
                    return $"{match.Groups["firstpart"]}.{match.Groups["extendTypeSize"]}";
                });

            Utils.CreateDirectoryForFileIfNeeded(targetStFilePath);

            File.WriteAllText(targetStFilePath, newContent, Encoding);

            Log?.Info($"Copied database physical structure file to {targetStFilePath.PrettyQuote()}.");

            return targetStFilePath;
        }

        /// <summary>
        /// Returns a connection string to use to connect to the given database.
        /// Use this method when the state of the database is unknown and we need o connect to it whether in single or multi user mode.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public UoeConnectionString GetConnectionString(UoeDatabase targetDb, string logicalName = null) {
            targetDb.ThrowIfNotExist();
            if (GetBusyMode(targetDb) == DatabaseBusyMode.NotBusy) {
                return UoeConnectionString.NewSingleUserConnection(targetDb, logicalName);
            }
            var logFilePath = Path.Combine(targetDb.DirectoryPath, $"{targetDb.PhysicalName}.lg");
            Log?.Debug($"Reading database log file to figure out the connection string: {logFilePath.PrettyQuote()}.");
            ReadLogFile(logFilePath, out string hostName, out string serviceName);
            if (string.IsNullOrEmpty(serviceName) || serviceName.Equals("0", StringComparison.Ordinal)) {
                serviceName = null;
                hostName = null;
            }
            return UoeConnectionString.NewMultiUserConnection(targetDb, logicalName, hostName, serviceName);
        }

        /// <summary>
        /// Kill a proserve process using the service name
        /// </summary>
        public static void KillAllMproSrv() {
            Process.GetProcesses()
                .Where(p => p.ProcessName.Contains("_mprosrv"))
                .ToList()
                .ForEach(p => p.Kill());
        }

        /// <summary>
        /// Returns the next free TCP port starting at the given port
        /// </summary>
        /// <param name="startingPort"></param>
        /// <returns></returns>
        public static int GetNextAvailablePort(int startingPort = 1024) {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var portArray = new List<int>();

            if (startingPort <= 0) {
                startingPort = 1;
            }

            // Ignore active connections
            portArray.AddRange(properties.GetActiveTcpConnections().Where(n => n.LocalEndPoint.Port >= startingPort).Select(n => n.LocalEndPoint.Port));

            // Ignore active tcp listners
            portArray.AddRange(properties.GetActiveTcpListeners().Where(n => n.Port >= startingPort).Select(n => n.Port));

            // Ignore active udp listeners
            portArray.AddRange(properties.GetActiveUdpListeners().Where(n => n.Port >= startingPort).Select(n => n.Port));

            for (var i = startingPort; i < ushort.MaxValue; i++) {
                if (!portArray.Contains(i)) {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Add the max connection try (-ct) parameter to a connection string.
        /// </summary>
        /// <param name="databaseConnectionString"></param>
        /// <param name="maxCt"></param>
        /// <returns></returns>
        public static string AddMaxConnectionTry(string databaseConnectionString, int maxCt = 1) {
            return databaseConnectionString?.Replace("-db", $"-ct {maxCt} -db");
        }


        protected ProcessIoWithLog GetExecutable(string exeName) {
            if (!_processIos.ContainsKey(exeName)) {
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                if (!File.Exists(outputPath)) {
                    throw new ArgumentException($"The openedge tool {exeName} does not exist in the expected path: {outputPath.PrettyQuote()}.");
                }

                _processIos.Add(exeName, new ProcessIoWithLog(outputPath) {
                    RedirectedOutputEncoding = Encoding,
                    CancelToken = CancelToken,
                    Log = Log
                });
            }

            _lastUsedProcess = _processIos[exeName];
            return _lastUsedProcess;
        }

        protected class ProcessIoWithLog : ProcessIo {

            /// <summary>
            /// A logger.
            /// </summary>
            public ILog Log { get; set; }

            public ProcessIoWithLog(string executablePath) : base(executablePath) { }

            protected override void PrepareStart(string arguments, bool silent) {
                base.PrepareStart(arguments, silent);
                _batchOutput = null;
                Log?.Debug($"Executing command:\n{ExecutedCommandLine}");
            }

            private string _batchOutput;

            /// <summary>
            /// Returns all the messages sent to the standard or error output, should be used once the process has exited
            /// </summary>
            public string BatchOutputString {
                get {
                    if (_batchOutput == null) {
                        var batchModeOutput = new StringBuilder();
                        foreach (var s in ErrorOutputArray.ToNonNullEnumerable()) {
                            batchModeOutput.AppendLine(s.Trim());
                        }
                        foreach (var s in StandardOutputArray.ToNonNullEnumerable()) {
                            batchModeOutput.AppendLine(s.Trim());
                        }
                        _batchOutput = batchModeOutput.ToString();
                    }

                    return _batchOutput;
                }
            }

            public override bool Execute(string arguments = null, bool silent = true, int timeoutMs = 0) {
                var result = base.Execute(arguments, silent, timeoutMs);
                Log?.Debug($"Command output:\n{BatchOutputString}");
                return result;
            }
        }

    }
}
