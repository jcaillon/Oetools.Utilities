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
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Database {
    
    /// <summary>
    /// Allows to interact with an openedge database at a file system level : create/start/shutdown and so on...
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
        public string LastOperationOutput => _lastUsedProcess?.BatchOutput?.ToString();

        private ProcessIo _lastUsedProcess;

        private Dictionary<string, ProcessIo> _processIos = new Dictionary<string, ProcessIo>();

        /// <summary>
        /// Returns the path to _dbutil (or null if not found in the dlc folder)
        /// </summary>
        private string DbUtilPath {
            get {
                string exeName = Utils.IsRuntimeWindowsPlatform ? "_dbutil.exe" : "_dbutil";
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                return File.Exists(outputPath) ? outputPath : null;
            }
        }

        /// <summary>
        /// Returns the path to _proutil (or null if not found in the dlc folder)
        /// </summary>
        private string ProUtilPath {
            get {
                string exeName = Utils.IsRuntimeWindowsPlatform ? "_proutil.exe" : "_proutil";
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                return File.Exists(outputPath) ? outputPath : null;
            }
        }

        /// <summary>
        /// Returns the path to _mprosrv (or null if not found in the dlc folder)
        /// </summary>
        private string ProservePath {
            get {
                string exeName = Utils.IsRuntimeWindowsPlatform ? "_mprosrv.exe" : "_mprosrv";
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                return File.Exists(outputPath) ? outputPath : null;
            }
        }

        /// <summary>
        /// Returns the path to _mprshut (or null if not found in the dlc folder)
        /// </summary>
        private string ProshutPath {
            get {
                string exeName = Utils.IsRuntimeWindowsPlatform ? "_mprshut.exe" : "_mprshut";
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                return File.Exists(outputPath) ? outputPath : null;
            }
        }
        
        /// <summary>
        /// The encoding to use for I/O of the openedge executables.
        /// </summary>
        private Encoding Encoding { get; set; } = Encoding.Default;

        /// <summary>
        /// New database utility
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
        /// Perform a prostrct create operation
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void ProstrctCreate(string targetDbPath, string structureFilePath, DatabaseBlockSize blockSize = DatabaseBlockSize.DefaultForCurrentPlatform) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            if (string.IsNullOrEmpty(structureFilePath)) {
                throw new UoeDatabaseOperationException("The structure file path can't be null.");
            }

            if (!File.Exists(structureFilePath)) {
                throw new UoeDatabaseOperationException($"The structure file does not exist : {structureFilePath.PrettyQuote()}.");
            }

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"prostrct create {dbPhysicalName} {structureFilePath.CliQuoter()} -blocksize {blockSize.ToString().Substring(1)}")) {
                throw new UoeDatabaseOperationException(GetErrorFromProcessIo(dbUtil));
            }
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
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            GetDatabaseFolderAndName(sourceDbPath, out string sourceDbFolder, out string sourceDbPhysicalName);

            if (string.IsNullOrEmpty(sourceDbPath)) {
                throw new UoeDatabaseOperationException("The source database can't be null.");
            }

            sourceDbPath = Path.Combine(sourceDbFolder, $"{sourceDbPhysicalName}.db");
            if (!File.Exists(sourceDbPath)) {
                throw new UoeDatabaseOperationException($"Could not find the procopy source database : {sourceDbPath.PrettyQuote()}.");
            }
            
            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"procopy {sourceDbPath.CliQuoter()} {dbPhysicalName}{(newInstance ? $" {NewInstanceFlag}" : "")}{(relativePath ? $" {RelativeFlag}" : "")}")) {
                throw new UoeDatabaseOperationException(GetErrorFromProcessIo(dbUtil));
            }
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
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            
            if (blockSize == DatabaseBlockSize.DefaultForCurrentPlatform) {
                blockSize = Utils.IsRuntimeWindowsPlatform ? DatabaseBlockSize.S4096 : DatabaseBlockSize.S8192;
            }

            string emptyDbFolder;
            if (!string.IsNullOrEmpty(codePage)) {
                emptyDbFolder = Path.Combine(DlcPath, "prolang", codePage);
                if (!Directory.Exists(emptyDbFolder)) {
                    throw new UoeDatabaseOperationException($"Invalid codepage, the folder doesn't exist : {emptyDbFolder.PrettyQuote()}.");
                }
            } else {
                emptyDbFolder = DlcPath;
            }

            var sourceDbPath = Path.Combine(emptyDbFolder, $"empty{(int) blockSize}");

            if (!File.Exists($"{sourceDbPath}.db")) {
                throw new UoeDatabaseOperationException($"Could not find the procopy source database : {sourceDbPath.PrettyQuote()}.");
            }
            
            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"procopy {sourceDbPath.CliQuoter()} {dbPhysicalName}{(newInstance ? $" {NewInstanceFlag}" : "")}{(relativePath ? $" {RelativeFlag}" : "")}") || !dbUtil.BatchOutput.ToString().Contains("(1365)")) {
                throw new UoeDatabaseOperationException(GetErrorFromProcessIo(dbUtil));
            }

            // db copied from C:\progress\client\v117x_dv\dlc\empty1. (1365)
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
                File.WriteAllText(stPath, "#\nb .\n#\nd \"Schema Area\":6,32;1 .\n#\nd \"Data Area\":7,256;1 .\n#\nd \"Index Area\":8,1;1 .", Encoding.ASCII);
            } catch (Exception e) {
                throw new UoeDatabaseOperationException($"Could not write .st file to {stPath.PrettyQuote()}.", e);
            }

            return stPath;
        }

        /// <summary>
        /// Prostrct repair operation
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void ProstrctRepair(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);
           
            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"prostrct repair {dbPhysicalName}") || !dbUtil.BatchOutput.ToString().Contains("(13485)")) {
                throw new UoeDatabaseOperationException(GetErrorFromProcessIo(dbUtil));
            }

            // RÚparation Prostrct de la base de donnÚes fuck Ó l'aide du fichier de structure fuck.st terminÚe. (13485)
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
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);
            
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

            GetExecutable(ProservePath);
            var proc = Process.Start(new ProcessStartInfo {
                FileName = ProservePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = options,
                WorkingDirectory = dbFolder
            });

            if (proc == null) {
                throw new UoeDatabaseOperationException($"Failed to start {ProservePath} {options}.");
            }
            
            do {
                busyMode = GetBusyMode(targetDbPath);
            } while (busyMode != DatabaseBusyMode.MultiUser && !proc.HasExited);

            if (busyMode != DatabaseBusyMode.MultiUser) {
                throw new UoeDatabaseOperationException($"Failed to serve the database, check the database log file {Path.Combine(dbFolder, $"{dbPhysicalName}.lg").PrettyQuote()}, options used : {options.PrettyQuote()}.");
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
                throw new UoeDatabaseOperationException(GetErrorFromProcessIo(proUtil));
            }

            var output = proUtil.BatchOutput.ToString();
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
        /// <param name="targetDbPath"></param>
        /// <param name="options"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void Proshut(string targetDbPath, string options = null) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var proshut = GetExecutable(ProshutPath);
            proshut.WorkingDirectory = dbFolder;
            proshut.TryExecute($"{dbPhysicalName} -by{(!string.IsNullOrEmpty(options) ? $" {options}" : string.Empty)}");
            
            if (GetBusyMode(targetDbPath) != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseOperationException(GetErrorFromProcessIo(proshut));
            }
        }

        /// <summary>
        /// Deletes the database, expects the database to be stopped first
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        public void Delete(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            var busyMode = GetBusyMode(targetDbPath);
            if (busyMode != DatabaseBusyMode.NotBusy) {
                throw new UoeDatabaseOperationException($"The database is still in use : {busyMode}.");
            }

            foreach (var file in Directory.EnumerateFiles(dbFolder, $"{dbPhysicalName}*", SearchOption.TopDirectoryOnly)) {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Generate a .st file at the given location, create all the needed AREA found in the given .df
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="sourceDfPath"></param>
        /// <exception cref="UoeDatabaseOperationException"></exception>
        /// <returns>file path to the created structure file</returns>
        public string GenerateStructureFileFromDf(string targetDbPath, string sourceDfPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            
            var stPath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");
            
            if (string.IsNullOrEmpty(sourceDfPath) || !File.Exists(sourceDfPath)) {
                throw new UoeDatabaseOperationException($"The file path for data definition file .df does not exist : {sourceDfPath.PrettyQuote()}.");
            }
            
            // https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dmadm/creating-a-structure-description-file.html
            // type ["areaname"[:areanum][,recsPerBlock][;blksPerCluster)]] path [extentType size]
            // type = (a | b | d | t) = a — After-image area, b — Before-image area, d — Schema and application data areas, t — Transaction log area
            // blksPerCluster = (1 | 8 | 64 | 512)
            // extentType = f | v
            // size = numeric value > 32

            var stContent = new StringBuilder("b .\n");
            stContent.Append("d \"Schema Area\" .\n");
            var areaAdded = new HashSet<string> { "Schema Area" };
            foreach (Match areaName in new Regex("AREA \"([^\"]+)\"").Matches(File.ReadAllText(sourceDfPath, Encoding.ASCII))) {
                if (!areaAdded.Contains(areaName.Groups[1].Value)) {
                    stContent.Append($"d {areaName.Groups[1].Value.CliQuoter()} .\n");
                    areaAdded.Add(areaName.Groups[1].Value);
                }
            }
            
            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }
            
            try {
                File.WriteAllText(stPath, stContent.ToString(), Encoding.ASCII);
            } catch (Exception e) {
                throw new UoeDatabaseOperationException($"Could not write .st file to {stPath.PrettyQuote()}", e);
            }

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
                throw new UoeDatabaseOperationException($"Invalid file path for source .st : {stFilePath.PrettyQuote()}.");
            }

            var newContent = new Regex("^(?<firstpart>\\w\\s+(\"[^\"]+\"(:\\d+)?(,\\d+)?(;\\d+)?)?\\s+)(?<path>\\S+|\"[^\"]+\")(?<extendTypeSize>(\\s+\\w+\\s+\\d+)?\\s*)$", RegexOptions.Multiline)
                .Replace(File.ReadAllText(stFilePath), match => {
                    return $"{match.Groups["firstpart"]}.{match.Groups["extendTypeSize"]}";
                });
            
            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }
            
            File.WriteAllText(stPath, newContent);

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
        /// <param name="serviceName"></param>
        /// <param name="hostname"></param>
        /// <param name="logicalName"></param>
        /// <returns></returns>
        public static string GetMultiUserConnectionString(string targetDbPath, string serviceName = null, string hostname = null, string logicalName = null) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            if (serviceName == null) {
                return $"-db {Path.Combine(dbFolder, $"{dbPhysicalName}.db").CliQuoter()} -ld {logicalName ?? dbPhysicalName}";
            }
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
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            return $"-db {Path.Combine(dbFolder, $"{dbPhysicalName}.db")} -ld {logicalName ?? dbPhysicalName} -1";
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
                throw new UoeDatabaseOperationException($"The logical name of the database is too long (>{DbLogicalNameMaxLength}) : {logicalName.PrettyQuote()}.");
            }
            if (logicalName.Any(c => !c.IsAsciiLetter() && !char.IsDigit(c) && c != '_' && c != '-')) {
                throw new UoeDatabaseOperationException($"The logical name of the database contains forbidden characters (should only contain english letters and numbers, underscore (_), and dash (-) characters) : {logicalName.PrettyQuote()}.");
            }
            if (!logicalName[0].IsAsciiLetter()) {
                throw new UoeDatabaseOperationException($"The logical name of a database should start with a english letter : {logicalName.PrettyQuote()}.");
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

        private static string GetHostName() {
            try {
                var hostname = Dns.GetHostName();
                Dns.GetHostEntry(hostname);
                return hostname;
            } catch (Exception) {
                return "localhost";
            }
        }
        
        protected ProcessIo GetExecutable(string path) {
            if (!_processIos.ContainsKey(path)) {
                if (string.IsNullOrEmpty(path)) {
                    throw new ArgumentException("Path can't be null.");
                }

                if (!File.Exists(path)) {
                    throw new ArgumentException($"Invalid path {path.PrettyQuote()}.");
                }

                _processIos.Add(path, new ProcessIo(path) {
                    RedirectedOutputEncoding = Encoding
                });
            }

            _lastUsedProcess = _processIos[path];
            return _lastUsedProcess;
        }

        /// <exception cref="UoeDatabaseOperationException"></exception>
        protected static void GetDatabaseFolderAndName(string dbPath, out string dbFolder, out string dbPhysicalName, bool needToExist = false) {
            if (string.IsNullOrEmpty(dbPath)) {
                throw new UoeDatabaseOperationException("Invalid path, can't be null.");
            }

            dbFolder = Path.GetDirectoryName(dbPath);

            if (string.IsNullOrEmpty(dbFolder)) {
                throw new UoeDatabaseOperationException("Database folder can't be null.");
            }

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            dbPhysicalName = Path.GetFileName(dbPath);

            if (dbPhysicalName.EndsWith(".db")) {
                dbPhysicalName = dbPhysicalName.Substring(0, dbPhysicalName.Length - 3);
            }
            
            if (dbPhysicalName.Any(c => !c.IsAsciiLetter() && !char.IsDigit(c) && c != '_' && c != '-')) {
                throw new UoeDatabaseOperationException($"The logical name of the database contains forbidden characters (should only contain english letters and numbers, underscore (_), and dash (-) characters) : {dbPhysicalName.PrettyQuote()}.");
            }
            
            if (dbPhysicalName.Length > DbPhysicalNameMaxLength) {
                throw new UoeDatabaseOperationException($"The physical name of the database is too long (>{DbPhysicalNameMaxLength}) : {dbPhysicalName.PrettyQuote()}.");
            }
            
            // doesn't exist?
            if (needToExist && !File.Exists(Path.Combine(dbFolder, $"{dbPhysicalName}.db"))) {
                throw new UoeDatabaseOperationException($"The target database doesn't exist, correct the target path : {dbPath.PrettyQuote()}.");
            }
        }
        
        protected static string GetErrorFromProcessIo(ProcessIo pro) {
            return $"{pro.BatchOutput.Trim()}";
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