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
using Oetools.Utilities.Openedge.Database.Exceptions;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Operate on an openedge database.
    /// </summary>
    public class UoeDatabaseOperator {

        /// <summary>
        /// Database server internationalization startup parameters such as -cpinternal codepage and -cpstream codepage.
        /// They will be used for commands that support them. (_dbutil, _mprosrv, _mprshut, _proutil)
        /// </summary>
        /// <remarks>
        /// https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dmadm%2Fdatabase-server-internationalization-parameters.html%23
        /// </remarks>
        public ProcessArgs InternationalizationStartupParameters { get; set; }

        /// <summary>
        /// The encoding to use for I/O of the openedge executables.
        /// </summary>
        public Encoding Encoding { get; }

        /// <summary>
        /// A logger.
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// Cancellation token. Used to cancel execution.
        /// </summary>
        public CancellationToken? CancelToken { get; set; }

        /// <summary>
        /// Path to the openedge installation folder
        /// </summary>
        protected string DlcPath { get; }

        private ProcessIo _lastUsedProcess;

        private Dictionary<string, ProcessIo> _processIos = new Dictionary<string, ProcessIo>();

        private string DbUtilName => Utils.IsRuntimeWindowsPlatform ? "_dbutil.exe" : "_dbutil";

        private string ProUtilName => Utils.IsRuntimeWindowsPlatform ? "_proutil.exe" : "_proutil";

        private string ProservePath => Path.Combine(DlcPath, "bin", Utils.IsRuntimeWindowsPlatform ? "_mprosrv.exe" : "_mprosrv");

        private string ProshutName => Utils.IsRuntimeWindowsPlatform ? "_mprshut.exe" : "_mprshut";

        /// <summary>
        /// New database utility.
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="encoding"></param>
        /// <exception cref="ArgumentException"></exception>
        public UoeDatabaseOperator(string dlcPath, Encoding encoding = null) {
            Encoding = encoding ?? UoeUtilities.GetProcessIoCodePageFromDlc(dlcPath);
            DlcPath = dlcPath;
            if (string.IsNullOrEmpty(dlcPath) || !Directory.Exists(dlcPath)) {
                throw new ArgumentException($"Invalid dlc path {dlcPath.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Validates whether or not the structure file is correct. Throws exception if not correct.
        /// </summary>
        /// <returns></returns>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void ValidateStructureFile(UoeDatabaseLocation targetDb, string structureFilePath) {
            structureFilePath = structureFilePath?.ToAbsolutePath();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Validating structure file {structureFilePath.PrettyQuote()}.");

            if (!targetDb.Exists()) {
                CreateVoidDatabase(targetDb, structureFilePath, DatabaseBlockSize.DefaultForCurrentPlatform, true);
            } else {
                AddStructureDefinition(targetDb, structureFilePath, true);
            }
        }

        /// <summary>
        /// Creates a void OpenEdge database from a previously defined structure description (.st) file.
        /// You will need to add a schema to this void database by using <see cref="CopyEmpty"/>.
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="validate"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        internal void CreateVoidDatabase(UoeDatabaseLocation targetDb, string structureFilePath, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, bool validate = false, ProcessArgs databaseAccessStartupParameters = null) {
            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            structureFilePath = structureFilePath?.ToAbsolutePath();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            // create necessary directories
            if (!Directory.Exists(targetDb.DirectoryPath)) {
                Directory.CreateDirectory(targetDb.DirectoryPath);
            }
            CreateExtentsDirectories(targetDb, structureFilePath);

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Creating database structure for {targetDb.FullPath.PrettyQuote()} from {structureFilePath.PrettyQuote()} with block size {blockSize.ToString().Substring(1)}.");

            var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("prostrct", "create", targetDb.PhysicalName, structureFilePath, "-blocksize", blockSize.ToString().Substring(1), validate ? "-validate" : null, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Create a new empty database.
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="codePage">Database codepage (copy from $DLC/prolang/codepage/emptyX)</param>
        /// <param name="newInstance">Specifies that a new GUID be created for the target database.</param>
        /// <param name="relativePath">Use relative path in the structure file.</param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Create(UoeDatabaseLocation targetDb, string structureFilePath = null, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true, ProcessArgs databaseAccessStartupParameters = null) {
            if (targetDb.Exists()) {
                throw new UoeDatabaseException($"The database already exists: {targetDb.FullPath.PrettyQuote()}.");
            }

            if (!string.IsNullOrEmpty(structureFilePath)) {
                CreateVoidDatabase(targetDb, structureFilePath, blockSize, false, databaseAccessStartupParameters);
            }
            CopyEmpty(targetDb, blockSize, codePage, newInstance, relativePath);
        }

        /// <summary>
        /// Copy an empty database from the openedge installation directory.
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="codePage">Database codepage (copy from $DLC/prolang/codepage/emptyX)</param>
        /// <param name="newInstance">Specifies that a new GUID be created for the target database.</param>
        /// <param name="relativePath">Use relative path in the structure file.</param>
        /// <exception cref="UoeDatabaseException"></exception>
        internal void CopyEmpty(UoeDatabaseLocation targetDb, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true) {
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

            var sourceDb = new UoeDatabaseLocation(Path.Combine(emptyDirectoryPath, $"empty{(int) blockSize}"));

            if (!sourceDb.Exists()) {
                throw new UoeDatabaseException($"Could not find the procopy source database: {sourceDb.FullPath.PrettyQuote()}.");
            }

            Copy(targetDb, sourceDb, newInstance, relativePath);
        }

        /// <summary>
        /// Copy a database (procopy).
        /// </summary>
        /// <param name="targetDb">Path to the target database</param>
        /// <param name="sourceDb">Path of the procopy source database</param>
        /// <param name="newInstance">Specifies that a new GUID be created for the target database.</param>
        /// <param name="relativePath">Use relative path in the structure file.</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Copy(UoeDatabaseLocation targetDb, UoeDatabaseLocation sourceDb, bool newInstance = true, bool relativePath = true) {
            sourceDb.ThrowIfNotExist();

            if (!Directory.Exists(targetDb.DirectoryPath)) {
                Directory.CreateDirectory(targetDb.DirectoryPath);
            }

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Copying database {sourceDb.FullPath.PrettyQuote()} to {targetDb.FullPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("procopy", sourceDb.FullPath, targetDb.PhysicalName, newInstance ? "-newinstance" : null, InternationalizationStartupParameters));
            if (!executionOk || !dbUtil.BatchOutputString.Contains("(1365)")) {
                // db copied from C:\progress\client\v117x_dv\dlc\empty1. (1365)
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }

            targetDb.ThrowIfNotExist();

            if (!File.Exists(targetDb.StructureFileFullPath) || relativePath) {
                UpdateStructureFile(targetDb, relativePath);
            }
        }

        /// <summary>
        /// Creates/updates a structure description (.st) file from the .db file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="relativePath">Use relative path in the structure file.</param>
        /// <param name="extentPathAsDirectory">By default, listing will output file path to each extent, this option allows to output directory path instead.</param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void UpdateStructureFile(UoeDatabaseLocation targetDb, bool relativePath = true, bool extentPathAsDirectory = true, ProcessArgs databaseAccessStartupParameters = null) {
            targetDb.ThrowIfNotExist();

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            var structureFilePath = Path.Combine(targetDb.DirectoryPath, $"{targetDb.PhysicalName}.st");
            Log?.Info($"{(File.Exists(structureFilePath) ? "Updating" : "Creating")} structure file (.st) for the database {targetDb.FullPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("prostrct", "list", targetDb.PhysicalName, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk || !File.Exists(targetDb.StructureFileFullPath)) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }

            if (extentPathAsDirectory || relativePath) {
                var dbDirectory = targetDb.DirectoryPath;

                // we can simplify the .st a bit because prostrct list will put the file path instead of the directory path for extents
                var newContent = new Regex(@"^(?<beforepath>(?<type>[abdt])(?<areainfo>\s""(?<areaname>[\w\s]+)""(:(?<areanum>[0-9]+))?(,(?<recsPerBlock>[0-9]+))?(;(?<blksPerCluster>[0-9]+))?)?\s)((?<path>[^\s""!]+)|!""(?<pathquoted>[^""]+)"")(?<afterpath>(\s(?<extentType>[f|v])\s(?<extentSize>[0-9]+))?)", RegexOptions.Multiline).Replace(File.ReadAllText(targetDb.StructureFileFullPath, Encoding), match => {
                    var path = match.Groups["pathquoted"].Value;
                    if (string.IsNullOrEmpty(path)) {
                        path = match.Groups["path"].Value;
                    }
                    if (!string.IsNullOrEmpty(path)) {
                        if (File.Exists(path.ToAbsolutePath(targetDb.DirectoryPath))) {
                            if (extentPathAsDirectory) {
                                var dir = Path.GetDirectoryName(path);
                                if (!string.IsNullOrEmpty(dir)) {
                                    path = dir;
                                }
                            }
                        } else {
                            path = path.TrimEndDirectorySeparator();
                        }
                        if (relativePath) {
                            path = path.ToRelativePath(dbDirectory, true);
                        }
                        path = path.Contains(' ') ? $"!\"{path}\"" : path;
                        return $"{match.Groups["beforepath"]}{path}{match.Groups["afterpath"]}";
                    }
                    return match.Value;
                });
                File.WriteAllText(targetDb.StructureFileFullPath, newContent, Encoding);
            }
        }

        /// <summary>
        /// Updates a database's control information (.db file) after an extent is moved or renamed.
        /// Does a prostrct repair or a prostrct builddb depending wether or not the .db exists.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void RepairDatabaseControlInfo(UoeDatabaseLocation targetDb, ProcessArgs databaseAccessStartupParameters = null) {
            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            if (!File.Exists(targetDb.StructureFileFullPath)) {
                throw new UoeDatabaseException($"The structure file does not exist: {targetDb.StructureFileFullPath.PrettyQuote()}.");
            }

            var repairMode = targetDb.Exists();

            Log?.Info($"{(repairMode ? "Repairing" : "Creating ")} database control information (.db) of {targetDb.FullPath.PrettyQuote()} from {targetDb.StructureFileFullPath}.");

            var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("prostrct", repairMode ? "repair" : "builddb", targetDb.PhysicalName, InternationalizationStartupParameters, databaseAccessStartupParameters));

            if (!executionOk || repairMode && !dbUtil.BatchOutputString.Contains("(13485)")) {
                // repair of *** ended. (13485)
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Appends the files from a new structure description (.st) file to a database.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="structureFilePath"></param>
        /// <param name="validate"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void AddStructureDefinition(UoeDatabaseLocation targetDb, string structureFilePath, bool validate = false, ProcessArgs databaseAccessStartupParameters = null) {
            targetDb.ThrowIfNotExist();

            structureFilePath = structureFilePath?.ToAbsolutePath();
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

            CreateExtentsDirectories(targetDb, structureFilePath);

            Log?.Info($"Appending new extents from {structureFilePath.PrettyQuote()} to the database structure of {targetDb.FullPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("prostrct", qualifier, targetDb.PhysicalName, structureFilePath, validate ? "-validate" : null, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk || dbUtil.BatchOutputString.Contains("(12867)")) {
                // prostrct add FAILED. (12867)
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }

            // we updated .db, now update the .st with the .db
            UpdateStructureFile(targetDb);
        }

        /// <summary>
        /// Removes storage areas or extents within storage areas.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="extentToken">Indicates the type of extent to remove. Specify one of the following: d, bi, ai, tl</param>
        /// <param name="storageArea">Specifies the name of the storage area to remove.</param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void RemoveStructureDefinition(UoeDatabaseLocation targetDb, string extentToken, string storageArea, ProcessArgs databaseAccessStartupParameters = null) {
            targetDb.ThrowIfNotExist();

            if (string.IsNullOrEmpty(extentToken)) {
                throw new UoeDatabaseException("The extent token can't be empty, it must be one of the following: d, bi, ai, tl.");
            }
            if (string.IsNullOrEmpty(storageArea)) {
                throw new UoeDatabaseException("The name of storage area to remove must be specified.");
            }

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Removing extent {extentToken.PrettyQuote()} named {storageArea.PrettyQuote()} from the database {targetDb.FullPath.PrettyQuote()}.");

            for (int i = 0; i < 2; i++) {
                var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("prostrct", "remove", targetDb.PhysicalName, extentToken, storageArea, InternationalizationStartupParameters, databaseAccessStartupParameters));
                if (!executionOk || !dbUtil.BatchOutputString.Contains("(6968)")) {
                    // successfully removed. (6968)
                    if (i == 0 && dbUtil.BatchOutputString.Contains("(6953)")) {
                        // You must use the proutil truncate bi command before doing a remove. (6953).
                        TruncateBi(targetDb);
                        continue;
                    }
                    throw new UoeDatabaseException(dbUtil.BatchOutputString);
                }
                break;
            }

            // we updated .db, now update the .st with the .db
            UpdateStructureFile(targetDb);
        }

        /// <summary>
        /// Start a database server
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="hostname"></param>
        /// <param name="serviceName"></param>
        /// <param name="nbUsers">Set the expected number of users that will use this db simultaneously, it will set the options to have only one broker starting with that broker being able to handle that many users.</param>
        /// <param name="options">https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dmadm/database-startup-parameters.html</param>
        /// <exception cref="UoeDatabaseException"></exception>
        /// <returns>start parameters string</returns>
        public UoeDatabaseConnection Start(UoeDatabaseLocation targetDb, string hostname = null, string serviceName = null, int? nbUsers = null, UoeProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            // check if busy
            DatabaseBusyMode busyMode = GetBusyMode(targetDb);
            if (busyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseException("Database already used in single user mode.");
            }
            if (busyMode == DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseException("Database already used in multi user mode.");
            }

            var args = new UoeProcessArgs();
            if (!string.IsNullOrEmpty(serviceName)) {
                args.Append("-S", serviceName);
            }

            if (!string.IsNullOrEmpty(hostname)) {
                args.Append("-N", "TCP", "-H", hostname);
            }

            if (nbUsers != null) {
                var mn = 1;
                var ma = nbUsers;
                args.Append("-n", mn * ma + 1, "-Mi", ma, "-Ma", ma, "-Mn", mn, "-Mpb", mn);
            }

            args.Append(targetDb.PhysicalName, options, InternationalizationStartupParameters);

            Log?.Info($"Starting database server for {targetDb.FullPath.PrettyQuote()}.");

            var proservePath = ProservePath;

            if (!File.Exists(proservePath)) {
                throw new UoeDatabaseException($"The proserve executable does not exist: {proservePath.PrettyQuote()}.");
            }

            var cliArgs = args.ToCliArgs();
            Log?.Debug($"Executing command:\n{ProcessArgs.ToCliArg(proservePath)} {cliArgs}");

            var proc = Process.Start(new ProcessStartInfo {
                FileName = proservePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = cliArgs,
                WorkingDirectory = targetDb.DirectoryPath
            });

            if (proc == null) {
                throw new UoeDatabaseException($"Failed to start {ProservePath.PrettyQuote()} with options: {args.ToString().PrettyQuote()}.");
            }

            do {
                busyMode = GetBusyMode(targetDb);
            } while (busyMode != DatabaseBusyMode.MultiUser && !proc.HasExited);

            if (busyMode != DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseException($"Failed to serve the database, check the database log file {Path.Combine(targetDb.DirectoryPath, $"{targetDb.PhysicalName}.lg").PrettyQuote()}, options used were: {args.ToString().PrettyQuote()}.");
            }

            return UoeDatabaseConnection.NewMultiUserConnection(targetDb, null, hostname, serviceName);
        }

        /// <summary>
        /// Returns the busy mode of the database, indicating if the database is used in single/multi user mode
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public DatabaseBusyMode GetBusyMode(UoeDatabaseLocation targetDb, ProcessArgs databaseAccessStartupParameters = null) {
            targetDb.ThrowIfNotExist();

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;
            proUtil.Log = null;
            try {
                proUtil.Execute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "busy", InternationalizationStartupParameters, databaseAccessStartupParameters));
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
        /// Shutdown a database started in multi user mode
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Shutdown(UoeDatabaseLocation targetDb, ProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            var proshut = GetExecutable(ProshutName);
            proshut.WorkingDirectory = targetDb.DirectoryPath;

            Log?.Info($"Shutting down database server for {targetDb.FullPath.PrettyQuote()}.");

            proshut.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-by", options, InternationalizationStartupParameters));

            if (GetBusyMode(targetDb) != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseException(proshut.BatchOutputString);
            }
        }

        /// <summary>
        /// Kill the broker of a database started in multi user mode
        /// </summary>
        /// <param name="processId"></param>
        /// <param name="targetDb"></param>
        /// <returns>success or not</returns>
        public bool KillBrokerServer(int processId, UoeDatabaseLocation targetDb = null) {
            if (processId > 0) {
                var mprosrv = Process.GetProcesses().FirstOrDefault(p => {
                    try {
                        return p.ProcessName.Contains("_mprosrv") && p.Id == processId;
                    } catch (Exception) {
                        return false;
                    }
                });
                if (mprosrv != null) {
                    Log?.Info($"Stopping database broker started on process id {processId}{(targetDb != null ? $" for {targetDb.FullPath.PrettyQuote()}" : "")}.");
                    mprosrv.Kill();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Deletes the database, expects the database to be stopped first. Does not delete the .st file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Delete(UoeDatabaseLocation targetDb) {
            targetDb.ThrowIfNotExist();

            var busyMode = GetBusyMode(targetDb);
            if (busyMode != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseException($"The database is still in use: {busyMode}.");
            }

            try {
                UpdateStructureFile(targetDb);
            } catch (Exception e) {
                Log?.Debug($"Caught exception while trying to generate the structure file: {e.Message}");
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
        /// <param name="structureFile"></param>
        internal void CreateExtentsDirectories(UoeDatabaseLocation targetDb, string structureFile) {
            if (!File.Exists(structureFile)) {
                throw new UoeDatabaseException($"The structure file does not exist: {structureFile}.");
            }

            foreach (var file in ListDatabaseFiles(targetDb, false, structureFile)) {
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
        /// <param name="structureFile"></param>
        /// <returns></returns>
        internal IEnumerable<String> ListDatabaseFiles(UoeDatabaseLocation targetDb, bool filesMustExist = true, string structureFile = null) {
            var stRegex = new Regex(@"^(?<type>[abdt])(?<areainfo>\s""(?<areaname>[\w\s]+)""(:(?<areanum>[0-9]+))?(,(?<recsPerBlock>[0-9]+))?(;(?<blksPerCluster>[0-9]+))?)?\s((?<path>[^\s""!]+)|!""(?<pathquoted>[^""]+)"")(\s(?<extentType>[f|v])\s(?<extentSize>[0-9]+))?", RegexOptions.Multiline);

            foreach (var ext in new List<string> { "lk", "lic", "lg", "db" }) {
                var path = Path.ChangeExtension(targetDb.FullPath, ext);
                if (!filesMustExist || File.Exists(path)) {
                    yield return path;
                }
            }

            structureFile = structureFile ?? targetDb.StructureFileFullPath;
            if (!File.Exists(structureFile)) {
                yield break;
            }

            var areas = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            var areaNumAuto = 6;
            foreach (Match match in stRegex.Matches(File.ReadAllText(structureFile))) {
                var path = match.Groups["pathquoted"].Value;
                if (string.IsNullOrEmpty(path)) {
                    path = match.Groups["path"].Value;
                }

                if (string.IsNullOrEmpty(path)) {
                    continue;
                }
                path = path.ToAbsolutePath(targetDb.DirectoryPath);

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
                var filePath = path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ? path : Path.Combine(path, fileName);

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
        public string GenerateStructureFileFromDf(UoeDatabaseLocation targetDb, string sourceDfPath) {
            sourceDfPath = sourceDfPath?.ToAbsolutePath();
            if (string.IsNullOrEmpty(sourceDfPath) || !File.Exists(sourceDfPath)) {
                throw new UoeDatabaseException($"The file path for data definition file .df does not exist: {sourceDfPath.PrettyQuote()}.");
            }

            var stContent = new StringBuilder("b .\n");
            stContent.Append("d \"Schema Area\" .\n");
            var areaAdded = new HashSet<string> { "Schema Area" };
            foreach (Match areaName in new Regex("AREA \"([^\"]+)\"").Matches(File.ReadAllText(sourceDfPath, Encoding))) {
                if (!areaAdded.Contains(areaName.Groups[1].Value)) {
                    stContent.Append($"d \"{areaName.Groups[1].Value}\" .\n");
                    areaAdded.Add(areaName.Groups[1].Value);
                }
            }

            try {
                File.WriteAllText(targetDb.StructureFileFullPath, stContent.ToString(), Encoding);
            } catch (Exception e) {
                throw new UoeDatabaseException($"Could not write {targetDb.StructureFileFullPath.PrettyQuote()}: {e.Message}.", e);
            }

            Log?.Info($"Generated database physical structure file {targetDb.StructureFileFullPath.PrettyQuote()} from schema definition file {sourceDfPath.PrettyQuote()}.");

            return targetDb.StructureFileFullPath;
        }

        /// <summary>
        /// Returns a connection string to use to connect to the given database.
        /// Use this method when the state of the database is unknown and we need o connect to it whether in single or multi user mode.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public UoeDatabaseConnection GetDatabaseConnection(UoeDatabaseLocation targetDb, string logicalName = null) {
            targetDb.ThrowIfNotExist();

            if (GetBusyMode(targetDb) == DatabaseBusyMode.NotBusy) {
                return UoeDatabaseConnection.NewSingleUserConnection(targetDb, logicalName);
            }

            var logFilePath = Path.Combine(targetDb.DirectoryPath, $"{targetDb.PhysicalName}.lg");
            Log?.Debug($"Reading database log file to figure out the connection string: {logFilePath.PrettyQuote()}.");
            ReadLogFile(logFilePath, out string hostName, out string serviceName, Encoding);
            if (string.IsNullOrEmpty(serviceName) || serviceName.Equals("0", StringComparison.Ordinal)) {
                serviceName = null;
                hostName = null;
            }
            return UoeDatabaseConnection.NewMultiUserConnection(targetDb, logicalName, hostName, serviceName);
        }

        /// <summary>
        /// Truncates the log file. If the database is started, it will re-log the database startup parameters to the log file.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void TruncateLog(UoeDatabaseLocation targetDb) {
            targetDb.ThrowIfNotExist();

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            bool online = false;
            if (GetBusyMode(targetDb) == DatabaseBusyMode.MultiUser) {
                online = true;
                Log?.Debug("The database is served for multi-users, using the `-online` option.");
            }

            try {
                dbUtil.Execute(new ProcessArgs().Append("prolog", targetDb.PhysicalName, online ? "-online" : null, InternationalizationStartupParameters));
            } catch(Exception) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Performs the following three functions:
        /// - Uses the information in the before-image (BI) files to bring the database and after-image (AI) files up to date, waits to verify that the information has been successfully written to the disk, then truncates the before-image file to its original length.
        /// - Sets the BI cluster size using the Before-image Cluster Size (-bi) parameter.
        /// - Sets the BI block size using the Before-image Block Size (-biblocksize) parameter.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <param name="options">{[ -G n]| -bi size| -biblocksize size }</param>
        /// <exception cref="UoeDatabaseException"></exception>
        public void TruncateBi(UoeDatabaseLocation targetDb, ProcessArgs databaseAccessStartupParameters = null, ProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            Log?.Info($"Truncate BI for the database {targetDb.FullPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "truncate", "bi", options, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk) {
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Rebuild the indexed of a database. By default, rebuilds all the active indexes.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="options">defaults to `activeindexes`.</param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void RebuildIndexes(UoeDatabaseLocation targetDb, ProcessArgs options = null, ProcessArgs databaseAccessStartupParameters = null) {
            if (options == null) {
                options = new ProcessArgs().Append("activeindexes");
            }

            targetDb.ThrowIfNotExist();

            Log?.Info($"Rebuilding the indexes of {targetDb.FullPath.PrettyQuote()} with the option {options}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "idxbuild", options, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk || !proUtil.BatchOutputString.Contains("(11465)")|| !proUtil.BatchOutputString.Contains(" 0 err")) {
                // 1 indexes were rebuilt. (11465)
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Dump binary data for the given table.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="tableName"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void DumpBinaryData(UoeDatabaseLocation targetDb, string tableName, string dumpDirectoryPath, ProcessArgs databaseAccessStartupParameters = null, ProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            if (string.IsNullOrEmpty(tableName)) {
                throw new UoeDatabaseException("The table name can't be null.");
            }
            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.ToAbsolutePath();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping binary data of table {tableName} to directory {dumpDirectoryPath.PrettyQuote()} from {targetDb.FullPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "dump", tableName, dumpDirectoryPath, options, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk || !proUtil.BatchOutputString.Contains("(6254)")) {
                // Binary Dump complete. (6254)
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Load a single binary data file to the given database.
        /// The index of the loaded tables should be rebuilt afterward.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="rebuildIndexes"></param>
        /// <param name="binDataFilePath"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public void LoadBinaryData(UoeDatabaseLocation targetDb, string binDataFilePath, bool rebuildIndexes = true, ProcessArgs databaseAccessStartupParameters = null, ProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            binDataFilePath = binDataFilePath?.ToAbsolutePath();
            if (!File.Exists(binDataFilePath)) {
                throw new UoeDatabaseException($"The binary data file does not exist: {binDataFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading binary data from {binDataFilePath.PrettyQuote()} to {targetDb.FullPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "load", binDataFilePath, options, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk || !proUtil.BatchOutputString.Contains("(6256)")) {
                if (options?.Contains("build indexes") ?? false) {
                    Log?.Warn("The build indexes option was used and the load has failed. Be aware that all the indexes are inactive.");
                }
                // Binary Load complete. (6256)
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
            if (rebuildIndexes && (!options?.Contains("build indexes") ?? false)) {
                RebuildIndexes(targetDb);
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
        /// <param name="options"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Backup(UoeDatabaseLocation targetDb, string targetBackupFile, bool verbose = true, bool scan = true, bool compressed = true, ProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            var busyMode = GetBusyMode(targetDb);
            if (busyMode == DatabaseBusyMode.SingleUser) {
                throw new ArgumentException($"The database is used in single user mode: {targetDb.FullPath.PrettyQuote()}.");
            }

            options?.Remove("-online");
            if (busyMode == DatabaseBusyMode.MultiUser) {
                // can't use -scan in online mode
                options?.Remove("-scan");
            } else if (scan) {
                options = options ?? new ProcessArgs().Append("-scan");
            }
            if (compressed) {
                options = options ?? new ProcessArgs().Append("-com");
            }
            if (verbose) {
                options = options ?? new ProcessArgs().Append("-verbose");
            }

            targetBackupFile = targetBackupFile.ToAbsolutePath();
            var dir = Path.GetDirectoryName(targetBackupFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            if (GetBusyMode(targetDb) == DatabaseBusyMode.NotBusy) {
                Log?.Debug($"Backing up database {targetDb.FullPath.PrettyQuote()} in offline mode to file {targetBackupFile.PrettyQuote()}.");

                var dbUtil = GetExecutable(DbUtilName);
                dbUtil.WorkingDirectory = targetDb.DirectoryPath;

                var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("probkup", targetDb.PhysicalName, targetBackupFile, options, InternationalizationStartupParameters));
                if (!executionOk || !dbUtil.BatchOutputString.Contains("(3740)")) {
                    // Backup complete. (3740)
                    throw new UoeDatabaseException(dbUtil.BatchOutputString);
                }
            } else {
                Log?.Debug($"Backing up database {targetDb.FullPath.PrettyQuote()} in online mode to file {targetBackupFile.PrettyQuote()}.");

                var proShut = GetExecutable(ProshutName);
                proShut.WorkingDirectory = targetDb.DirectoryPath;

                var executionOk = proShut.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "backup", "online", targetBackupFile, options, InternationalizationStartupParameters));
                if (!executionOk || !proShut.BatchOutputString.Contains("(3740)")) {
                    // Backup complete. (3740)
                    throw new UoeDatabaseException(proShut.BatchOutputString);
                }
            }
        }

        /// <summary>
        /// Restores a full or incremental backup of a database (or verifies the integrity of a database backup).
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="backupFile"></param>
        /// <param name="options"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="UoeDatabaseException"></exception>
        public void Restore(UoeDatabaseLocation targetDb, string backupFile, ProcessArgs options = null) {
            backupFile = backupFile.ToAbsolutePath();
            if (!File.Exists(backupFile)) {
                throw new UoeDatabaseException($"The backup file does not exist: {backupFile.PrettyQuote()}.");
            }

            Log?.Debug($"Restoring database {targetDb.FullPath.PrettyQuote()} from file {backupFile.PrettyQuote()}.");

            var dbUtil = GetExecutable(DbUtilName);
            dbUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = dbUtil.TryExecute(new ProcessArgs().Append("prorest", targetDb.PhysicalName, backupFile, options, InternationalizationStartupParameters));
            if (!executionOk) {
                throw new UoeDatabaseException(dbUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Loads text data files into a database.
        /// </summary>
        /// <param name="targetDb"></param>
        /// <param name="descriptionFile"></param>
        /// <param name="dataDirectoryPath"></param>
        /// <param name="databaseAccessStartupParameters">Database access/encryption parameters:  [[-userid username [-password passwd ]] | [ -U username -P passwd] ] [-Passphrase].</param>
        /// <param name="options"></param>
        /// <remarks>
        /// - BULKLOAD deactivates indexes on the tables being loaded. Indexes should be rebuilt before accessing data.
        /// - https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dmadm%2Fcreating-a-bulk-loader-description-file.html%23
        /// </remarks>
        /// <exception cref="UoeDatabaseException"></exception>
        public void BulkLoad(UoeDatabaseLocation targetDb, string descriptionFile, string dataDirectoryPath, ProcessArgs databaseAccessStartupParameters = null, ProcessArgs options = null) {
            targetDb.ThrowIfNotExist();

            descriptionFile = descriptionFile?.ToAbsolutePath();
            if (!File.Exists(descriptionFile)) {
                throw new UoeDatabaseException($"The bulk loader description file does not exist: {descriptionFile.PrettyQuote()}.");
            }

            Log?.Info($"Bulk loading data from {dataDirectoryPath.PrettyQuote()} to {targetDb.FullPath.PrettyQuote()} using the description file {descriptionFile.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilName);
            proUtil.WorkingDirectory = targetDb.DirectoryPath;

            var executionOk = proUtil.TryExecute(new ProcessArgs().Append(targetDb.PhysicalName, "-C", "bulkload", descriptionFile, options, "datadir", dataDirectoryPath, InternationalizationStartupParameters, databaseAccessStartupParameters));
            if (!executionOk) {
                throw new UoeDatabaseException(proUtil.BatchOutputString);
            }
        }

        /// <summary>
        /// Reads information from a database log file.
        /// </summary>
        /// <param name="logFilePath"></param>
        /// <param name="hostName"></param>
        /// <param name="serviceName"></param>
        /// <param name="encoding"></param>
        internal static void ReadLogFile(string logFilePath, out string hostName, out string serviceName, Encoding encoding = null) {
            // read the log file in reverse order, trying to get the hostname and service name used to start the database.
            hostName = null;
            serviceName = null;
            if (!File.Exists(logFilePath)) {
                return;
            }
            foreach (var line in new ReverseLineReader(logFilePath, encoding ?? Encoding.ASCII)) {
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

        protected ProcessIo GetExecutable(string exeName) {
            if (!_processIos.ContainsKey(exeName)) {
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                if (!File.Exists(outputPath)) {
                    throw new ArgumentException($"The openedge tool {exeName} does not exist in the expected path: {outputPath.PrettyQuote()}.");
                }
                _processIos.Add(exeName, new ProcessIo(outputPath) {
                    RedirectedOutputEncoding = Encoding,
                    CancelToken = CancelToken,
                    Log = Log
                });
            }
            _lastUsedProcess = _processIos[exeName];
            return _lastUsedProcess;
        }

    }
}
