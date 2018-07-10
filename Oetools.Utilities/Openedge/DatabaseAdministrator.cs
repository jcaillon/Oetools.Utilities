#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (DatabaseAdministrator.cs) is part of Oetools.Utilities.
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
using System.Net;
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge {
    
    public class DatabaseAdministrator : DatabaseOperator {

        /// <summary>
        /// The temp folder to use when we need to write the openedge procedure for data administration
        /// </summary>
        public string TempFolder { get; set; } = Path.GetTempPath();
        
        public DatabaseAdministrator(string dlcPath) : base(dlcPath) { }
        
        /// <summary>
        /// Load a .df in a database
        /// </summary>
        /// <param name="targetDbPath">Path to the target database</param>
        /// <param name="dfFilePath">Path to the .st file, a prostrct create will be executed with it to create the database</param>
        /// <returns></returns>
        /// <exception cref="DatabaseOperationException"></exception>
        public void LoadDf(string targetDbPath, string dfFilePath) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName, true);

            if (string.IsNullOrEmpty(dfFilePath)) {
                throw new DatabaseOperationException("The structure file path can't be null");
            }

            if (!File.Exists(dfFilePath)) {
                throw new DatabaseOperationException($"The structure file does not exist : {dfFilePath}");
            }

            if (GetBusyMode(targetDbPath) != DatabaseBusyMode.NotBusy) {
                throw new DatabaseOperationException("The database is currently busy, shut it down before this operation");
            }

            var progres = GetExecutable(ProgresPath);
            progres.WorkingDirectory = dbFolder;

            using (var proc = new DatabaseAdministratorProcedure(TempFolder)) {
                var executionOk = progres.TryExecute($"-b -db {dbPhysicalName}.db -1 -ld DICTDB -p {proc.ProcedurePath.Quoter()} -param {$"load-df|{dfFilePath}".Quoter()}");
                if (!executionOk || progres.StandardOutput.ToString().EndsWith("**ERROR")) {
                    throw new DatabaseOperationException(GetErrorFromProcessIo(progres));
                }
             }
        }

        /// <summary>
        /// Returns the connection string to use to connect to a database
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="serviceName"></param>
        /// <param name="singleUser"></param>
        /// <returns></returns>
        public static string GetConnectionString(string targetDbPath, string serviceName, bool singleUser = false) {
            GetDatabaseFolderAndName(targetDbPath, out string dbFolder, out string dbPhysicalName);
            if (singleUser) {
                return $"-db {Path.Combine(dbFolder, $"{dbPhysicalName}.db")} -ld {dbPhysicalName} -1";
            }
            return $"-db {dbPhysicalName} -ld {dbPhysicalName} -N TCP -H localhost -S {serviceName}";
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
        
        private class DatabaseAdministratorProcedure : IDisposable {
            
            public DatabaseAdministratorProcedure(string folderPath) {
                ProcedurePath = Path.Combine(folderPath, $"db_admin_{Path.GetRandomFileName()}.p");
                File.WriteAllText(ProcedurePath, OpenedgeResources.GetOpenedgeAsStringFromResources(@"database_administrator.p"));
            }

            public string ProcedurePath { get; }
            
            public void Dispose() {
                File.Delete(ProcedurePath);
            }
        }
    }
}