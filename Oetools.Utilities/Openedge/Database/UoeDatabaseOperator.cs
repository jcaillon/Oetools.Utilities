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

        private const int DbPhysicalNameMaxLength = 11;
        private const int DbLogicalNameMaxLength = 32;
        private const string NewInstanceFlag = "-newinstance";
        private const string RelativeFlag = "-relative";

        /// <summary>
        /// Path to the openedge installation folder
        /// </summary>
        protected string DlcPath { get; }

        /// <summary>
        /// Returns the output value of the last operation done
        /// </summary>
        public string LastOperationOutput => GetBatchOutputFromProcessIo(_lastUsedProcess);

        private ProcessIo _lastUsedProcess;

        private Dictionary<string, ProcessIo> _processIos = new Dictionary<string, ProcessIo>();

        /// <summary>
        /// Returns the path to _dbutil (or null if not found in the dlc folder)
        /// </summary>
        private string DbUtilPath => Utils.IsRuntimeWindowsPlatform ? "_dbutil.exe" : "_dbutil";

        /// <summary>
        /// Returns the path to _proutil (or null if not found in the dlc folder)
        /// </summary>
        private string ProUtilPath => Utils.IsRuntimeWindowsPlatform ? "_proutil.exe" : "_proutil";

        /// <summary>
        /// Returns the path to _mprosrv (or null if not found in the dlc folder)
        /// </summary>
        private string ProservePath => Utils.IsRuntimeWindowsPlatform ? "_mprosrv.exe" : "_mprosrv";

        /// <summary>
        /// Returns the path to _mprshut (or null if not found in the dlc folder)
        /// </summary>
        private string ProshutPath => Utils.IsRuntimeWindowsPlatform ? "_mprshut.exe" : "_mprshut";

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
        /// Truncates the log file. If the database is started, it will re-log the database startup parameters to the log file.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void TruncateLog(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;

            string online =  null;
            if (GetBusyMode(targetDbPath) == DatabaseBusyMode.MultiUser) {
                online = " -online";
                Log?.Debug("The database is served for multi-users, using the `-online` option.");
            }

            try {
                dbUtil.Execute($"prolog {dbPhysicalName}{online}");
            } catch(Exception) {
                throw new UoeDatabaseOperationException(GetBatchOutputFromProcessIo(dbUtil));
            }
        }

        /// <summary>
        /// Dump binary data for the given table.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="dumpDirectoryPath"></param>
        /// <param name="options"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void DumpBinaryData(string targetDbPath, string tableName, string dumpDirectoryPath, string options = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            if (string.IsNullOrEmpty(dumpDirectoryPath)) {
                throw new UoeDatabaseOperationException("The data dump directory path can't be null.");
            }
            dumpDirectoryPath = dumpDirectoryPath.MakePathAbsolute();
            if (!string.IsNullOrEmpty(dumpDirectoryPath) && !Directory.Exists(dumpDirectoryPath)) {
                Directory.CreateDirectory(dumpDirectoryPath);
            }

            Log?.Info($"Dumping binary data of table {tableName} to directory {dumpDirectoryPath.PrettyQuote()} from {targetDbPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilPath);
            proUtil.WorkingDirectory = dbFolder;

            var executionOk = proUtil.TryExecute($"{dbPhysicalName} -C dump {tableName} {dumpDirectoryPath.CliQuoter()} {options}");
            var batchOutput = GetBatchOutputFromProcessIo(proUtil);
            if (!executionOk || !batchOutput.Contains("(6254)")) {
                // Binary Dump complete. (6254)
                throw new UoeDatabaseOperationException(batchOutput);
            }

            Log?.Debug(batchOutput);
        }

        /// <summary>
        /// Load binary data to the given database.
        /// The index of the loaded tables should be rebuilt afterward.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="options"></param>
        /// <param name="binDataFilePath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void LoadBinaryData(string targetDbPath, string binDataFilePath, string options = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            binDataFilePath = binDataFilePath?.MakePathAbsolute();
            if (!File.Exists(binDataFilePath)) {
                throw new UoeDatabaseOperationException($"The binary data file does not exist: {binDataFilePath.PrettyQuote()}.");
            }

            Log?.Info($"Loading binary data from {binDataFilePath.PrettyQuote()} to {targetDbPath.PrettyQuote()}.");

            var proUtil = GetExecutable(ProUtilPath);
            proUtil.WorkingDirectory = dbFolder;

            var executionOk = proUtil.TryExecute($"{dbPhysicalName} -C load {binDataFilePath.CliQuoter()} {options}");
            var batchOutput = GetBatchOutputFromProcessIo(proUtil);
            if (!executionOk || !batchOutput.Contains("(6256)")) {
                // Binary Load complete. (6256)
                throw new UoeDatabaseOperationException(batchOutput);
            }

            Log?.Debug(batchOutput);
        }

        /// <summary>
        /// Rebuild the indexed of a database. By default, rebuilds all the active indexes.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void RebuildIndexes(string targetDbPath, string options = "activeindexes") {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var proUtil = GetExecutable(ProUtilPath);
            proUtil.WorkingDirectory = dbFolder;

            var executionOk = proUtil.TryExecute($"{dbPhysicalName} -C idxbuild {options}");
            var batchOutput = GetBatchOutputFromProcessIo(proUtil);
            if (!executionOk || !batchOutput.Contains("(11465)")|| !batchOutput.Contains(" 0 err")) {
                // 1 indexes were rebuilt. (11465)
                throw new UoeDatabaseOperationException(batchOutput);
            }

            Log?.Debug(batchOutput);
        }

        /// <summary>
        /// Creates a void OpenEdge database from a previously defined structure description (.st) file.
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="extra"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void ProstrctCreate(string targetDbPath, string structureFilePath, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string extra = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            structureFilePath = structureFilePath?.MakePathAbsolute();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseOperationException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            CreateExtentsDirectories(structureFilePath);

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;

            Log?.Info($"Creating database structure for {targetDbPath.PrettyQuote()} from {structureFilePath.PrettyQuote()} with block size {blockSize.ToString().Substring(1)}.");

            var executionOk = dbUtil.TryExecute($"prostrct create {dbPhysicalName} {structureFilePath.CliQuoter()} -blocksize {blockSize.ToString().Substring(1)} {extra}");
            if (!executionOk) {
                throw new UoeDatabaseOperationException(GetBatchOutputFromProcessIo(dbUtil));
            }

            Log?.Debug(GetBatchOutputFromProcessIo(dbUtil));
        }

        /// <summary>
        /// Creates a structure description (.st) file for an OpenEdge database.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="extra"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void ProstrctList(string targetDbPath, string extra = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;

            Log?.Info($"Creating structure file for the database {targetDbPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"prostrct list {dbPhysicalName} {extra}");
            if (!executionOk) {
                throw new UoeDatabaseOperationException(GetBatchOutputFromProcessIo(dbUtil));
            }

            Log?.Debug(GetBatchOutputFromProcessIo(dbUtil));
        }

        /// <summary>
        /// Updates a database's control information (.db file) after an extent is moved or renamed.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="extra"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void ProstrctRepair(string targetDbPath, string extra = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;

            Log?.Info($"Repairing database structure of {targetDbPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"prostrct repair {dbPhysicalName} {extra}");
            var batchOutput = GetBatchOutputFromProcessIo(dbUtil);
            if (!executionOk || !batchOutput.Contains("(13485)")) {
                // repair of *** ended. (13485)
                throw new UoeDatabaseOperationException(batchOutput);
            }

            Log?.Debug(batchOutput);
        }

        /// <summary>
        /// Appends the files from a new structure description (.st) file to a database.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="structureFilePath"></param>
        /// <param name="extra"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void ProstrctAdd(string targetDbPath, string structureFilePath, string extra = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            structureFilePath = structureFilePath?.MakePathAbsolute();
            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseOperationException($"The structure file does not exist: {structureFilePath.PrettyQuote()}.");
            }

            string qualifier = "add";
            if (GetBusyMode(targetDbPath) == DatabaseBusyMode.MultiUser) {
                qualifier = "addonline";
                Log?.Debug($"The database is served for multi-users, using the `{qualifier}` qualifier.");
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;

            Log?.Info($"Appending new extents from {structureFilePath.PrettyQuote()} to the database structure of {targetDbPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"prostrct {qualifier} {dbPhysicalName} {structureFilePath.CliQuoter()} {extra}");
            var batchOutput = GetBatchOutputFromProcessIo(dbUtil);
            if (!executionOk || batchOutput.Contains("(12867)")) {
                // prostrct add FAILED. (12867)
                throw new UoeDatabaseOperationException(batchOutput);
            }

            Log?.Debug(batchOutput);
        }

        /// <summary>
        /// Performs a procopy operation
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="sourceDbPath">Path of the procopy source database</param>
        /// <param name="newInstance">Use -newinstance in procopy command</param>
        /// <param name="relativePath">Use -relativepath in procopy command</param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void Procopy(string targetDbPath, string sourceDbPath, bool newInstance = true, bool relativePath = true) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            sourceDbPath = GetDatabaseFolderAndName(sourceDbPath, out string _, out string _, true);

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;

            var structureFilePath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");
            CreateExtentsDirectories(structureFilePath);

            Log?.Info($"Copying database {sourceDbPath.PrettyQuote()} to {targetDbPath.PrettyQuote()}.");

            var executionOk = dbUtil.TryExecute($"procopy {sourceDbPath.CliQuoter()} {dbPhysicalName}{(newInstance ? $" {NewInstanceFlag}" : "")}{(relativePath ? $" {RelativeFlag}" : "")}");
            var batchOutput = GetBatchOutputFromProcessIo(dbUtil);
            if (!executionOk || !batchOutput.Contains("(1365)")) {
                // db copied from C:\progress\client\v117x_dv\dlc\empty1. (1365)
                throw new UoeDatabaseOperationException(batchOutput);
            }

            Log?.Debug(batchOutput);
        }

        /// <summary>
        /// Performs a procopy operation
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <param name="codePage">Database codepage (copy from $DLC/prolang/codepage/emptyX)</param>
        /// <param name="newInstance">Use -newinstance in procopy command</param>
        /// <param name="relativePath">Use -relativepath in procopy command</param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void Procopy(string targetDbPath, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform, string codePage = null, bool newInstance = true, bool relativePath = true) {
            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            string emptyDbFolder;
            if (!string.IsNullOrEmpty(codePage)) {
                emptyDbFolder = Path.Combine(DlcPath, "prolang", codePage);
                if (!Directory.Exists(emptyDbFolder)) {
                    throw new UoeDatabaseOperationException($"Invalid codepage, the folder doesn't exist: {emptyDbFolder.PrettyQuote()}.");
                }
            } else {
                emptyDbFolder = DlcPath;
            }

            var sourceDbPath = Path.Combine(emptyDbFolder, $"empty{(int) blockSize}.db").MakePathAbsolute();

            if (!File.Exists(sourceDbPath)) {
                throw new UoeDatabaseOperationException($"Could not find the procopy source database: {sourceDbPath.PrettyQuote()}.");
            }

            Procopy(targetDbPath, sourceDbPath, newInstance, relativePath);
        }

        /// <summary>
        /// Creates a predefined structure file for type 2 storage area database
        /// </summary>
        /// <param name="targetDbPath"></param>
        public string CreateStandardStructureFile(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var stPath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            try {
                File.WriteAllText(stPath, $"#\nb .\n#\nd \"Schema Area\":6,{(Utils.IsRuntimeWindowsPlatform ? 32 : 64).ToString()};1 .\n#\nd \"Data Area\":7,32;1 .\n#\nd \"Index Area\":8,32;1 .", Encoding.ASCII);
            } catch (Exception e) {
                throw new UoeDatabaseOperationException($"Could not write .st file to {stPath.PrettyQuote()}.", e);
            }

            Log?.Info($"Created standard structure file {stPath.PrettyQuote()}.");

            return stPath;
        }

        /// <summary>
        /// Start a databaser server
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <returns>start parameters string</returns>
        public string ProServe(string targetDbPath, int? nbUsers = null, string options = null) {
            return ProServe(targetDbPath, null, nbUsers, options);
        }

        /// <summary>
        /// Start a databaser server
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="servicePort"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <returns>start parameters string</returns>
        public string ProServe(string targetDbPath, int servicePort, int? nbUsers = null, string options = null) {
            return ProServe(targetDbPath, servicePort.ToString(), nbUsers, options);
        }

        /// <summary>
        /// Start a database server
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="serviceName"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        /// <returns>start parameters string</returns>
        public string ProServe(string targetDbPath, string serviceName, int? nbUsers = null, string options = null) {
            return ProServe(targetDbPath, null, serviceName, nbUsers, options);
        }

        /// <summary>
        /// Start a database server
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="hostname"></param>
        /// <param name="serviceName"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        /// <returns>start parameters string</returns>
        public string ProServe(string targetDbPath, string hostname, string serviceName, int? nbUsers = null, string options = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            if (nbUsers != null) {
                var mn = 1;
                var ma = nbUsers;
                var userOptions = $"-n {mn * ma + 1} -Mi {ma} -Ma {ma} -Mn {mn} -Mpb {mn}";
                options = options == null ? userOptions : $"{userOptions} {options}";
            }

            // check if busy
            DatabaseBusyMode busyMode = GetBusyMode(targetDbPath);
            if (busyMode == DatabaseBusyMode.SingleUser) {
                throw new UoeDatabaseOperationException("Database already used in single user mode.");
            }
            if (busyMode == DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseOperationException("Database already used in multi user mode.");
            }

            if (!string.IsNullOrEmpty(hostname)) {
                options = $"-N TCP -H {hostname} {options ?? ""}";
            }

            if (!string.IsNullOrEmpty(serviceName)) {
                options = $"-S {serviceName} {options ?? ""}";
            }

            options = $"{dbPhysicalName} {options ?? ""}".CliCompactWhitespaces();

            Log?.Info($"Starting database server for {targetDbPath.PrettyQuote()} with options: {options.PrettyQuote()}.");

            var proc = Process.Start(new ProcessStartInfo {
                FileName = ProservePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = options,
                WorkingDirectory = dbFolder
            });

            if (proc == null) {
                throw new UoeDatabaseOperationException($"Failed to start {ProservePath.PrettyQuote()} with options: {options.PrettyQuote()}.");
            }

            do {
                busyMode = GetBusyMode(targetDbPath);
            } while (busyMode != DatabaseBusyMode.MultiUser && !proc.HasExited);

            if (busyMode != DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseOperationException($"Failed to serve the database, check the database log file {Path.Combine(dbFolder, $"{dbPhysicalName}.lg").PrettyQuote()}, options used: {options.PrettyQuote()}.");
            }

            return options;
        }

        /// <summary>
        /// Returns the busy mode of the database, indicating if the database is used in single/multi user mode
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public DatabaseBusyMode GetBusyMode(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var proUtil = GetExecutable(ProUtilPath);
            proUtil.WorkingDirectory = dbFolder;
            try {
                proUtil.Execute($"{dbPhysicalName} -C busy");
            } catch(Exception) {
                throw new UoeDatabaseOperationException(GetBatchOutputFromProcessIo(proUtil));
            }

            var output = GetBatchOutputFromProcessIo(proUtil);
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
        /// Returns a connection string to use to connect to the given database.
        /// Use this method when the state of the database is unknown and we need o connect to it whether in single or multi user mode.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public string GetConnectionString(string targetDbPath, string logicalName = null) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);
            if (GetBusyMode(targetDbPath) == DatabaseBusyMode.NotBusy) {
                return GetSingleUserConnectionString(targetDbPath, logicalName);
            }
            var logFilePath = Path.Combine(dbFolder, $"{dbPhysicalName}.lg");
            Log?.Debug($"Reading database log file to figure out the connection string: {logFilePath.PrettyQuote()}.");
            ReadLogFile(logFilePath, out string hostName, out string serviceName);
            if (string.IsNullOrEmpty(serviceName) || serviceName.Equals("0", StringComparison.Ordinal)) {
                serviceName = null;
                hostName = null;
            }
            return GetMultiUserConnectionString(targetDbPath, hostName, serviceName, logicalName);
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
                        // BROKER  0: (4261)  Host Name (-H): localhost
                        idx = line.IndexOf(':', idx + 6);
                        hostName = idx > 0 && line.Length > idx ? line.Substring(idx + 1).Trim() : null;
                        // If not -H was specified when starting the db, the -H will equal to the current hostname.
                        // But you can't connect with this hostname so we correct it here.
                        if (!string.IsNullOrEmpty(hostName) && hostName.Equals(GetHostName(), StringComparison.OrdinalIgnoreCase)) {
                            hostName = "localhost";
                        }
                        return;
                    case "4262":
                        // BROKER  0: (4262)  Service Name (-S): 0
                        idx = line.IndexOf(':', idx + 6);
                        serviceName = idx > 0 && line.Length > idx ? line.Substring(idx + 1).Trim() : null;
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
        /// <param name="targetDbPath"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void Proshut(string targetDbPath, string options = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var proshut = GetExecutable(ProshutPath);
            proshut.WorkingDirectory = dbFolder;

            Log?.Info($"Shutting down database server for {targetDbPath.PrettyQuote()}.");

            proshut.TryExecute($"{dbPhysicalName} -by{(!string.IsNullOrEmpty(options) ? $" {options}" : string.Empty)}");

            if (GetBusyMode(targetDbPath) != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseOperationException(GetBatchOutputFromProcessIo(proshut));
            }

            Log?.Debug(GetBatchOutputFromProcessIo(proshut));
        }

        /// <summary>
        /// Deletes the database, expects the database to be stopped first. Does not delete the .st file.
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void Delete(string targetDbPath) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var busyMode = GetBusyMode(targetDbPath);
            if (busyMode != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseOperationException($"The database is still in use: {busyMode}.");
            }

            var stPath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");
            if (!File.Exists(stPath)) {
                Log?.Debug($"The structure file does not exist, creating it: {stPath.PrettyQuote()}");
                ProstrctList(targetDbPath);
            }

            Log?.Info($"Deleting database files for {targetDbPath.PrettyQuote()} using the content of {dbPhysicalName}.st.");

            foreach (var file in ListDatabaseFiles(stPath)) {
                Log?.Debug($"Deleting: {file.PrettyQuote()}.");
                File.Delete(file);
            }

        }

        /// <summary>
        /// Creates the necessary directories to create the extents listed in the .st file.
        /// </summary>
        /// <param name="stPath"></param>
        public void CreateExtentsDirectories(string stPath) {
            foreach (var file in ListDatabaseFiles(stPath, false)) {
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
        /// <param name="stPath"></param>
        /// <param name="filesMustExist"></param>
        /// <returns></returns>
        public IEnumerable<String> ListDatabaseFiles(string stPath, bool filesMustExist = true) {
            var dbFolder = Path.GetDirectoryName(stPath);
            var dbPhysicalName = Path.GetFileNameWithoutExtension(stPath);

            var stRegex = new Regex(@"^(?<type>[abdt])(?<areainfo>\s""(?<areaname>[\w\s]+)""(:(?<areanum>[0-9]+))?(,(?<recsPerBlock>[0-9]+))?(;(?<blksPerCluster>[0-9]+))?)?\s((?<path>[^\s""!]+)|!""(?<pathquoted>[^""]+)"")(\s(?<extentType>[f|v])\s(?<extentSize>[0-9]+))?", RegexOptions.Multiline);

            foreach (var ext in new List<string> { "lk", "lic", "lg", "db" }) {
                var path = Path.ChangeExtension(stPath, ext);
                if (!filesMustExist || File.Exists(path)) {
                    yield return path;
                }
            }

            if (string.IsNullOrEmpty(stPath) || !File.Exists(stPath)) {
                yield break;
            }

            var areas = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            var areaNumAuto = 6;
            foreach (Match match in stRegex.Matches(File.ReadAllText(stPath))) {
                var directory = match.Groups["pathquoted"].Value;
                if (string.IsNullOrEmpty(directory)) {
                    directory = match.Groups["path"].Value;
                }

                if (string.IsNullOrEmpty(directory)) {
                    continue;
                }
                directory = directory.MakePathAbsolute(dbFolder);

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
                var fileName = $"{dbPhysicalName}{suffix}.{areaType}{areas[areaId]}";
                var filePath = directory.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ? directory : Path.Combine(directory, fileName);

                if (!filesMustExist || File.Exists(filePath)) {
                    yield return filePath;
                }
            }
        }

        /// <summary>
        /// Generates a .st file at the given location, create all the needed AREA found in the given .df
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="sourceDfPath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        /// <returns>file path to the created structure file</returns>
        public string GenerateStructureFileFromDf(string targetDbPath, string sourceDfPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            sourceDfPath = sourceDfPath?.MakePathAbsolute();
            if (string.IsNullOrEmpty(sourceDfPath) || !File.Exists(sourceDfPath)) {
                throw new UoeDatabaseOperationException($"The file path for data definition file .df does not exist: {sourceDfPath.PrettyQuote()}.");
            }

            // https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dmadm/creating-a-structure-description-file.html
            // type ["areaname"[:areanum][,recsPerBlock][;blksPerCluster)]] path [extentType size]
            // type = (a | b | d | t) = a — After-image area, b — Before-image area, d — Schema and application data areas, t — Transaction log area
            // blksPerCluster = (1 | 8 | 64 | 512)
            // extentType = f | v
            // size = numeric value > 32
            // @"^(?<type>[abdt])(?<areainfo>\s""(?<areaname>[\w\s]+)""(:(?<areanum>[0-9]+))?(,(?<recsPerBlock>[0-9]+))?(;(?<blksPerCluster>[0-9]+))?)?\s((?<path>[^\s""!]+)|!""(?<pathquoted>[^""]+)"")(\s(?<extentType>[f|v])\s(?<extentSize>[0-9]+))?"

            var stContent = new StringBuilder("b .\n");
            stContent.Append("d \"Schema Area\" .\n");
            var areaAdded = new HashSet<string> { "Schema Area" };
            foreach (Match areaName in new Regex("AREA \"([^\"]+)\"").Matches(File.ReadAllText(sourceDfPath, Encoding))) {
                if (!areaAdded.Contains(areaName.Groups[1].Value)) {
                    stContent.Append($"d {areaName.Groups[1].Value.CliQuoter()} .\n");
                    areaAdded.Add(areaName.Groups[1].Value);
                }
            }

            var stPath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            try {
                File.WriteAllText(stPath, stContent.ToString(), Encoding.ASCII);
            } catch (Exception e) {
                throw new UoeDatabaseOperationException($"Could not write .st file to {stPath.PrettyQuote()}", e);
            }

            Log?.Info($"Generated database physical structure file {stPath.PrettyQuote()} from schema definition file {sourceDfPath.PrettyQuote()}.");

            return stPath;
        }

        /// <summary>
        /// Copy a source .st file to the target database directory, replacing any specific path by the relative path "."
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="stFilePath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public string CopyStructureFile(string targetDbPath, string stFilePath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var stPath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");

            if (string.IsNullOrEmpty(stFilePath) || !File.Exists(stFilePath)) {
                throw new UoeDatabaseOperationException($"Invalid file path for source .st: {stFilePath.PrettyQuote()}.");
            }

            var newContent = new Regex("^(?<firstpart>\\w\\s+(\"[^\"]+\"(:\\d+)?(,\\d+)?(;\\d+)?)?\\s+)(?<path>\\S+|\"[^\"]+\")(?<extendTypeSize>(\\s+\\w+\\s+\\d+)?\\s*)$", RegexOptions.Multiline)
                .Replace(File.ReadAllText(stFilePath, Encoding), match => {
                    return $"{match.Groups["firstpart"]}.{match.Groups["extendTypeSize"]}";
                });

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            File.WriteAllText(stPath, newContent, Encoding);

            Log?.Info($"Copied database physical structure file to {stPath.PrettyQuote()}.");

            return stPath;
        }

        /// <summary>
        /// Returns true if the given database can be found
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public static bool DatabaseExists(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            return File.Exists(Path.Combine(dbFolder, $"{dbPhysicalName}.db"));
        }

        /// <summary>
        /// Returns the directory in which the given database is located
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <returns></returns>
        public static string GetDatabaseDirectory(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out _);
            return dbFolder;
        }

        /// <summary>
        /// Returns the physical name of the database (without extension)
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <returns></returns>
        public static string GetDatabasePhysicalName(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out _, out string dbPhysicalName);
            return dbPhysicalName;
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
        /// Returns the multi user connection string to use to connect to a locally hosted database
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="hostname"></param>
        /// <param name="serviceName"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        public static string GetMultiUserConnectionString(string targetDbPath, string hostname = null, string serviceName = null, string logicalName = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string _, out string dbPhysicalName);
            if (serviceName == null) {
                // self service (shared memory) mode.
                return $"-db {targetDbPath.CliQuoter()} -ld {logicalName ?? dbPhysicalName}";
            }
            // network mode.
            return $"-db {dbPhysicalName} -ld {logicalName ?? dbPhysicalName} -N TCP -H {hostname ?? "localhost"} -S {serviceName}";
        }

        /// <summary>
        /// Returns the single user connection string to use to connect to a database
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public static string GetSingleUserConnectionString(string targetDbPath, string logicalName = null) {
            targetDbPath = GetDatabaseFolderAndName(targetDbPath, out string _, out string dbPhysicalName);
            return $"-db {targetDbPath.CliQuoter()} -ld {logicalName ?? dbPhysicalName} -1";
        }

        /// <summary>
        /// Throws exceptions if the given logical name is invalid
        /// </summary>
        /// <param name="logicalName"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public static void ValidateLogicalName(string logicalName) {
            if (string.IsNullOrEmpty(logicalName)) {
                throw new UoeDatabaseOperationException("The logical name of the database is null or empty.");
            }
            if (logicalName.Length > DbLogicalNameMaxLength) {
                throw new UoeDatabaseOperationException($"The logical name of the database is too long (>{DbLogicalNameMaxLength}): {logicalName.PrettyQuote()}.");
            }
            if (logicalName.Any(c => !c.IsAsciiLetter() && !char.IsDigit(c) && c != '_' && c != '-')) {
                throw new UoeDatabaseOperationException($"The logical name of the database contains forbidden characters (should only contain english letters and numbers, underscore (_), and dash (-) characters) : {logicalName.PrettyQuote()}.");
            }
            if (!logicalName[0].IsAsciiLetter()) {
                throw new UoeDatabaseOperationException($"The logical name of a database should start with a english letter: {logicalName.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Returns a valid logical name from a string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetValidLogicalName(string input) {
            if (string.IsNullOrEmpty(input)) {
                return "unnamed";
            }
            var output = new StringBuilder();
            foreach (var character in input) {
                if (character.IsAsciiLetter() || char.IsDigit(character) || character == '_' || character == '-') {
                    output.Append(character);
                }
                if (output.Length >= DbLogicalNameMaxLength) {
                    break;
                }
            }
            return output.Length > 0 ? output.ToString() : "unnamed";
        }

        /// <summary>
        /// Returns a valid physical name from a string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetValidPhysicalName(string input) {
            var output = GetValidLogicalName(input);
            return output.Length > DbPhysicalNameMaxLength ? output.Substring(0, DbPhysicalNameMaxLength) : output;
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

        protected ProcessIo GetExecutable(string exeName) {
            if (!_processIos.ContainsKey(exeName)) {
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                if (!File.Exists(outputPath)) {
                    throw new ArgumentException($"The openedge tool {exeName} does not exist in the expected path: {outputPath.PrettyQuote()}.");
                }

                _processIos.Add(exeName, new ProcessIo(outputPath) {
                    RedirectedOutputEncoding = Encoding,
                    CancelToken = CancelToken
                });
            }

            _lastUsedProcess = _processIos[exeName];
            return _lastUsedProcess;
        }

        /// <exception cref="UoeDatabaseOperationException"></exception>
        protected static string GetDatabaseFolderAndName(string dbPath, out string dbFolder, out string dbPhysicalName, bool needToExist = false) {
            if (string.IsNullOrEmpty(dbPath)) {
                throw new UoeDatabaseOperationException("Invalid path, can't be null.");
            }

            dbPath = dbPath.MakePathAbsolute();
            dbFolder = Path.GetDirectoryName(dbPath);

            if (string.IsNullOrEmpty(dbFolder)) {
                throw new UoeDatabaseOperationException("Database folder can't be null.");
            }

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            dbPhysicalName = Path.GetFileName(dbPath);

            if (string.IsNullOrEmpty(dbPhysicalName)) {
                throw new UoeDatabaseOperationException($"The physical name of the database is empty: {dbPath.PrettyQuote()}.");
            }

            if (dbPhysicalName.EndsWith(".db")) {
                dbPhysicalName = dbPhysicalName.Substring(0, dbPhysicalName.Length - 3);
            }

            if (dbPhysicalName.Any(c => !c.IsAsciiLetter() && !char.IsDigit(c) && c != '_' && c != '-')) {
                throw new UoeDatabaseOperationException($"The logical name of the database contains forbidden characters (should only contain english letters and numbers, underscore (_), and dash (-) characters): {dbPhysicalName.PrettyQuote()}.");
            }

            if (dbPhysicalName.Length > DbPhysicalNameMaxLength) {
                throw new UoeDatabaseOperationException($"The physical name of the database is too long (>{DbPhysicalNameMaxLength}): {dbPhysicalName.PrettyQuote()}.");
            }

            // doesn't exist?
            if (needToExist && !File.Exists(Path.Combine(dbFolder, $"{dbPhysicalName}.db"))) {
                throw new UoeDatabaseOperationException($"The database doesn't exist, correct the path: {dbPath.PrettyQuote()}.");
            }

            return Path.Combine(dbFolder, $"{dbPhysicalName}.db");
        }

        protected static string GetBatchOutputFromProcessIo(ProcessIo pro) {
            var batchModeOutput = new StringBuilder();
            foreach (var s in pro.ErrorOutputArray.ToNonNullEnumerable()) {
                batchModeOutput.AppendLine(s.Trim());
            }
            batchModeOutput.TrimEnd();
            foreach (var s in pro.StandardOutputArray.ToNonNullEnumerable()) {
                batchModeOutput.AppendLine(s.Trim());
            }
            batchModeOutput.TrimEnd();
            return batchModeOutput.ToString();
        }

    }

    /// <summary>
    /// Describes the block size of a database
    /// </summary>
    public enum DatabaseBlockSize : byte {
        DefaultForCurrentPlatform = 0,
        S1024 = 1,
        S2048 = 2,
        S4096 = 4,
        S8192 = 8
    }

    public enum DatabaseBusyMode {
        NotBusy,
        SingleUser,
        MultiUser
    }
}
