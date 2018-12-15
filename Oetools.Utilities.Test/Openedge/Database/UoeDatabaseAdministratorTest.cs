#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseAdministratorTest.cs) is part of Oetools.Utilities.Test.
// 
// Oetools.Utilities.Test is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities.Test is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities.Test. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Database;

namespace Oetools.Utilities.Test.Openedge.Database {
    
    [TestClass]
    public class UoeDatabaseAdministratorTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeDatabaseAdministratorTest)));

        [ClassInitialize]
        public static void Init(TestContext context) {           
            Cleanup();
            Directory.CreateDirectory(TestFolder);
        }
        
        [ClassCleanup]
        public static void Cleanup() {
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }
        
        [TestMethod]
        public void Load_df_Ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var db = new UoeDatabaseOperator(dlcPath);
            db.Procopy(Path.Combine(TestFolder, "ref.db"), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "ref.db")));
            
            // create .df
            var dfPath = Path.Combine(TestFolder, "ref.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.LoadDf(Path.Combine(TestFolder, "ref.db"), dfPath);
            }
        }
        
        [TestMethod]
        public void Load_df_Should_Fail() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var db = new UoeDatabaseOperator(dlcPath);
            db.Procopy(Path.Combine(TestFolder, "ref2.db"), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "ref2.db")));
            
            // create .df
            var dfPath = Path.Combine(TestFolder, "ref2.df");
            File.WriteAllText(dfPath, "ADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10");

            Exception ex = null;
            try {
                using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                    dataAdmin.LoadDf(Path.Combine(TestFolder, "ref2.db"), dfPath);
                }
            } catch (Exception e) {
                ex = e;
            }
            Assert.IsNotNull(ex);
        }

        [TestMethod]
        public void CreateDatabase() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateDatabase(Path.Combine(TestFolder, "created1.db"));
                Assert.IsTrue(dataAdmin.GetBusyMode(Path.Combine(TestFolder, "created1.db")).Equals(DatabaseBusyMode.NotBusy));
            }
            
            // create .df
            var dfPath = Path.Combine(TestFolder, "ref.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");
            
            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateDatabase(Path.Combine(TestFolder, "created2.db"), dataAdmin.CreateStandardStructureFile(Path.Combine(TestFolder, "created2.db")), DatabaseBlockSize.S2048, null, true, true, dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(Path.Combine(TestFolder, "created2.db")).Equals(DatabaseBusyMode.NotBusy));
            }
            
        }

        [TestMethod]
        public void CreateCompilationDatabase() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            // create .df
            var dfPath = Path.Combine(TestFolder, "compil.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");
            
            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateCompilationDatabaseFromDf(Path.Combine(TestFolder, "compil.db"), dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(Path.Combine(TestFolder, "compil.db")).Equals(DatabaseBusyMode.NotBusy));
            }
            
        }
        
    }
}