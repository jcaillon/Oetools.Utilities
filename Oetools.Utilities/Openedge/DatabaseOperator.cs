#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (Db.cs) is part of Oetools.Utilities.
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
using System.Net.NetworkInformation;
using System.Text;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge {
    
    /// <summary>
    /// Allows to interact with an openedge database at a file system level : create/start/shutdown and so on...
    /// </summary>
    public class DatabaseOperator {
        
        private const int DbNameMaxLength = 11;
        private const string NewInstanceFlag = "-newinstance";
        private const string RelativeFlag = "-relative";

        /// <summary>
        /// Path to the openedge installation folder
        /// </summary>
        protected string DlcPath { get; }

        /// <summary>
        /// Returns the standard output value of the last operation done
        /// </summary>
        public string LastOperationStandardOutput => _lastUsedProcess?.StandardOutput?.ToString();

        private ProcessIo _lastUsedProcess;

        private Dictionary<string, ProcessIo> _processIos = new Dictionary<string, ProcessIo>(StringComparer.CurrentCultureIgnoreCase);

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
        /// Returns the path to _progres (or null if not found in the dlc folder)
        /// </summary>
        protected string ProgresPath {
            get {
                string exeName = Utils.IsRuntimeWindowsPlatform ? "_progres.exe" : "_progres";
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
        /// New database utility
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <exception cref="ArgumentException"></exception>
        public DatabaseOperator(string dlcPath) {
            DlcPath = dlcPath;
            if (string.IsNullOrEmpty(dlcPath) || !Directory.Exists(dlcPath)) {
                throw new ArgumentException($"Invalid dlc path {dlcPath ?? "null"}");
            }
        }

        /// <summary>
        /// Perform a prostrct create operation
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="structureFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <param name="blockSize">Block size for the database prostrct create</param>
        /// <returns></returns>
        /// <exception cref="DatabaseOperationException"></exception>
        public void ProstrctCreate(string targetDbPath, string structureFilePath, DatabaseBlockSize blockSize) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            if (string.IsNullOrEmpty(structureFilePath)) {
                throw new DatabaseOperationException("The structure file path can't be null");
            }

            if (!File.Exists(structureFilePath)) {
                throw new DatabaseOperationException($"The structure file does not exist : {structureFilePath}");
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"prostrct create {dbPhysicalName} {structureFilePath.Quoter()} -blocksize {blockSize.ToString().Substring(1)}")) {
                throw new DatabaseOperationException($"Error : {dbUtil.ErrorOutput}\nStandard output was : {dbUtil.StandardOutput}");
            }
        }

        /// <summary>
        /// Performs a procopy operation
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="sourceDbPath">Path of the procopy source database</param>
        /// <param name="newInstance">Use -newinstance in procopy command</param>
        /// <param name="relativePath">Use -relativepath in procopy command</param>
        /// <exception cref="DatabaseOperationException"></exception>
        public void Procopy(string targetDbPath, string sourceDbPath, bool newInstance = true, bool relativePath = true) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            GetDatabaseFolderAndName(sourceDbPath, out string sourceDbFolder, out string sourceDbPhysicalName);

            if (string.IsNullOrEmpty(sourceDbPath)) {
                throw new DatabaseOperationException($"The source database can't be null");
            }

            sourceDbPath = Path.Combine(sourceDbFolder, $"{sourceDbPhysicalName}.db");
            if (!File.Exists(sourceDbPath)) {
                throw new DatabaseOperationException($"Could not find the procopy source database : {sourceDbPath}");
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"procopy {sourceDbPath.Quoter()} {dbPhysicalName}{(newInstance ? $" {NewInstanceFlag}" : "")}{(relativePath ? $" {RelativeFlag}" : "")}")) {
                throw new DatabaseOperationException($"Error : {dbUtil.ErrorOutput}\nStandard output was : {dbUtil.StandardOutput}");
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
        /// <exception cref="DatabaseOperationException"></exception>
        public void Procopy(string targetDbPath, DatabaseBlockSize blockSize, string codePage = null, bool newInstance = true, bool relativePath = true) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            string emptyDbFolder;
            if (!string.IsNullOrEmpty(codePage)) {
                emptyDbFolder = Path.Combine(DlcPath, "prolang", codePage);
                if (!Directory.Exists(emptyDbFolder)) {
                    throw new DatabaseOperationException($"Invalid codepage, the folder doesn't exist : {emptyDbFolder}");
                }
            } else {
                emptyDbFolder = DlcPath;
            }

            var sourceDbPath = Path.Combine(emptyDbFolder, $"empty{(int) blockSize}");

            if (!File.Exists($"{sourceDbPath}.db")) {
                throw new DatabaseOperationException($"Could not find the procopy source database : {sourceDbPath}");
            }

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"procopy {sourceDbPath.Quoter()} {dbPhysicalName}{(newInstance ? $" {NewInstanceFlag}" : "")}{(relativePath ? $" {RelativeFlag}" : "")}") || !dbUtil.StandardOutput.ToString().ContainsFast("(1365)")) {
                throw new DatabaseOperationException($"Error : {dbUtil.ErrorOutput}\nStandard output was : {dbUtil.StandardOutput}");
            }

            // La base de donnÚes a ÚtÚ copiÚe depuis C:\progress\client\v117x_dv\dlc\empty1. (1365)
        }

        /// <summary>
        /// Creates a predefined structure file for type 2 storage area database
        /// </summary>
        /// <param name="targetDbPath"></param>
        public string CreateStandardStructureFile(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var stPath = Path.Combine(dbFolder, $"{dbPhysicalName}.st");

            try {
                File.WriteAllText(stPath, "#\nb .\n#\nd \"Schema Area\":6,32;1 .\n#\nd \"Data Area\":7,256;1 .\n#\nd \"Index Area\":8,1;1 .", Encoding.ASCII);
            } catch (Exception e) {
                throw new DatabaseOperationException($"Could not write .st file to {stPath}", e);
            }

            return stPath;
        }

        /// <summary>
        /// Prostrct repair operation
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <exception cref="DatabaseOperationException"></exception>
        public void ProstrctRepair(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var dbUtil = GetExecutable(DbUtilPath);
            dbUtil.WorkingDirectory = dbFolder;
            if (!dbUtil.TryExecute($"prostrct repair {dbPhysicalName}") || !dbUtil.StandardOutput.ToString().ContainsFast("(13485)")) {
                throw new DatabaseOperationException($"Error : {dbUtil.ErrorOutput}\nStandard output was : {dbUtil.StandardOutput}");
            }

            // RÚparation Prostrct de la base de donnÚes fuck Ó l'aide du fichier de structure fuck.st terminÚe. (13485)
        }

        /// <summary>
        /// Start a databaser server
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="servicePort"></param>
        /// <param name="nbUsers"></param>
        /// <param name="options"></param>
        public void ProServe(string targetDbPath, int servicePort, int? nbUsers = null, string options = null) {
            if (nbUsers != null) {
                var mn = 1;
                var ma = nbUsers;
                var userOptions = $"-n {mn * ma + 1} -Mi {ma} -Ma {ma} -Mn {mn} -Mpb {mn}";
                options = options == null ? userOptions : $"{userOptions} {options}";
            }

            ProServe(targetDbPath, servicePort.ToString(), options);
        }

        /// <summary>
        /// Start a database server
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="serviceName"></param>
        /// <param name="options"></param>
        /// <exception cref="DatabaseOperationException"></exception>
        public void ProServe(string targetDbPath, string serviceName, string options = null) {
            if (string.IsNullOrEmpty(serviceName)) {
                throw new DatabaseOperationException("The service name/port can't be null");
            }

            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            // check if busy
            DatabaseBusyMode busyMode = GetBusyMode(targetDbPath);
            if (busyMode == DatabaseBusyMode.SingleUser) {
                throw new DatabaseOperationException($"Database already used in single user mode");
            } 
            if (busyMode == DatabaseBusyMode.MultiUser) {
                throw new DatabaseOperationException($"Database already used in multi user mode");
            }
            
            options = $"{dbPhysicalName} -S {serviceName} {options ?? ""}".CompactWhitespaces();

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
                throw new DatabaseOperationException($"Failed to start {ProservePath} {options}");
            }
            
            busyMode = DatabaseBusyMode.NotBusy;
            while (busyMode != DatabaseBusyMode.MultiUser && !proc.HasExited) {
                busyMode = GetBusyMode(targetDbPath);
            }

            if (busyMode != DatabaseBusyMode.MultiUser) {
                throw new DatabaseOperationException($"Failed to server the database, check the database log file {Path.Combine(dbFolder, $"{dbPhysicalName}.lg")}");
            }
        }

        /// <summary>
        /// Returns the busy mode of the database, indicating if the database is used in single/multi user mode
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseOperationException"></exception>
        public DatabaseBusyMode GetBusyMode(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var proUtil = GetExecutable(ProUtilPath);
            proUtil.WorkingDirectory = dbFolder;
            try {
                proUtil.Execute($"{dbPhysicalName} -C busy");
            } catch(Exception) {
                throw new DatabaseOperationException($"Error : {proUtil.ErrorOutput}\nStandard output was : {proUtil.StandardOutput}");
            }

            var output = proUtil.StandardOutput.ToString();
            switch (output) {
                case string _ when output.ContainsFast("(276)"):
                    return DatabaseBusyMode.MultiUser;
                case string _ when output.ContainsFast("(263)"):
                    return DatabaseBusyMode.SingleUser;
                default:
                    return DatabaseBusyMode.NotBusy;
            }
        }

        /// <summary>
        /// Shutdown a database started in multi user mode, will not fail if the db dosn't exist or is not started
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <exception cref="DatabaseOperationException"></exception>
        public void Proshut(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var proshut = GetExecutable(ProshutPath);
            proshut.WorkingDirectory = dbFolder;
            proshut.TryExecute($"{dbPhysicalName} -by");
            
            if (GetBusyMode(targetDbPath) != DatabaseBusyMode.NotBusy) {
                throw new DatabaseOperationException($"Error : {proshut.ErrorOutput}\nStandard output was : {proshut.StandardOutput}");
            }
        }

        /// <summary>
        /// Deletes the database, expects the database to be stopped first
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <exception cref="DatabaseOperationException"></exception>
        public void Delete(string targetDbPath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);

            var busyMode = GetBusyMode(targetDbPath);
            if (busyMode != DatabaseBusyMode.NotBusy) {
                throw new DatabaseOperationException($"The database is still in use : {busyMode}");
            }

            foreach (var file in Directory.EnumerateFiles(dbFolder, $"{dbPhysicalName}*", SearchOption.TopDirectoryOnly)) {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Generate a .st file at the given location, create all the needed AREA found in the given .df
        /// </summary>
        /// <param name="targetStPath"></param>
        /// <param name="sourceDfPath"></param>
        /// <exception cref="DatabaseOperationException"></exception>
        public void GenerateStructureFileFromDf(string targetStPath, string sourceDfPath) {
            // https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dmadm/creating-a-structure-description-file.html
            
            throw new NotImplementedException();
            
            // TODO
            try {
                File.WriteAllText(targetStPath, "#\nb .\n#\nd \"Schema Area\":6,32;1 .\n#\nd \"Data Area\":7,256;1 .\n#\nd \"Index Area\":8,1;1 .", Encoding.ASCII);
            } catch (Exception e) {
                throw new DatabaseOperationException($"Could not write .st file to {targetStPath}", e);
            }            
        }

        /// <summary>
        /// Kill a proserve process using the service name
        /// </summary>
        public static void KillAllMproSrv() {
            Process.GetProcesses()
                .Where(p => p.ProcessName.ContainsFast("_mprosrv"))
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
        
        protected ProcessIo GetExecutable(string path) {
            if (!_processIos.ContainsKey(path)) {
                if (string.IsNullOrEmpty(path)) {
                    throw new ArgumentException("Path can't be null");
                }

                if (!File.Exists(path)) {
                    throw new ArgumentException($"Invalid path {path}");
                }

                _processIos.Add(path, new ProcessIo(path));
            }

            _lastUsedProcess = _processIos[path];
            return _lastUsedProcess;
        }

        protected void GetDatabaseFolderAndName(string dbPath, out string dbFolder, out string dbPhysicalName) {
            if (string.IsNullOrEmpty(dbPath)) {
                throw new DatabaseOperationException("Invalid path, can't be null");
            }

            dbFolder = Path.GetDirectoryName(dbPath);

            if (string.IsNullOrEmpty(dbFolder)) {
                throw new DatabaseOperationException("Database folder can't be null");
            }

            if (!Directory.Exists(dbFolder)) {
                Directory.CreateDirectory(dbFolder);
            }

            dbPhysicalName = Path.GetFileName(dbPath);

            if (dbPhysicalName.EndsWith(".db")) {
                dbPhysicalName = dbPhysicalName.Substring(0, dbPhysicalName.Length - 3);
            }

            if (dbPhysicalName.Length > DbNameMaxLength) {
                throw new DatabaseOperationException($"The physical name of the database is too long (>{DbNameMaxLength}) : {dbPhysicalName}");
            }
        }

    }

    public enum DatabaseBlockSize {
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