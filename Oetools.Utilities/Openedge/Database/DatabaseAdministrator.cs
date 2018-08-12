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

using System;
using System.IO;
using System.Net;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Database {
    
    public class DatabaseAdministrator : DatabaseOperator, IDisposable {

        /// <summary>
        /// The temp folder to use when we need to write the openedge procedure for data administration
        /// </summary>
        public string TempFolder { get; set; } = Utils.GetTempDirectory();
        
        public DatabaseAdministrator(string dlcPath) : base(dlcPath) { }

        private ProgressProcessIo _progres;
        
        private string _procedurePath;

        private string ProcedurePath {
            get {
                if (_procedurePath == null) {
                    _procedurePath = Path.Combine(TempFolder, $"db_admin_{Path.GetRandomFileName()}.p");
                    File.WriteAllText(ProcedurePath, OpenedgeResources.GetOpenedgeAsStringFromResources(@"database_administrator.p"));
                }
                return _procedurePath;
            }
        }

        private ProgressProcessIo Progres {
            get {
                if (_progres == null) {
                    _progres = new ProgressProcessIo(DlcPath, true);
                }
                return _progres;
            }
        }

        /// <summary>
        /// Generates a database from a .df (database definition) file
        /// that database should not be used in production since it has all default configuration, its purpose is to exist for file compilation
        /// </summary>
        /// <param name="targetDbPath"></param>
        /// <param name="dfFilePath"></param>
        public void CreateCompilationDatabaseFromDf(string targetDbPath, string dfFilePath) {
            ProstrctCreate(targetDbPath, GenerateStructureFileFromDf(targetDbPath, dfFilePath));
            Procopy(targetDbPath);
            LoadDf(targetDbPath, dfFilePath);
        }
        
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
                throw new DatabaseOperationException($"The structure file does not exist : {dfFilePath.PrettyQuote()}");
            }

            if (GetBusyMode(targetDbPath) != DatabaseBusyMode.NotBusy) {
                throw new DatabaseOperationException("The database is currently busy, shut it down before this operation");
            }

            Progres.WorkingDirectory = dbFolder;

            var executionOk = Progres.TryExecute($"-db {dbPhysicalName}.db -1 -ld DICTDB -p {ProcedurePath.CliQuoter()} -param {$"load-df|{dfFilePath}".CliQuoter()}");
            if (!executionOk || Progres.BatchOutput.ToString().EndsWith("**ERROR")) {
                throw new DatabaseOperationException(Progres.BatchOutput.ToString());
            }
        }
        
        public void Dispose() {
            _progres?.Dispose();
            _progres = null;
            File.Delete(ProcedurePath);
        }
    }
}