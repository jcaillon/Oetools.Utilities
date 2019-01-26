#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionDbExtractTableAndSequenceListTest.cs) is part of Oetools.Utilities.Test.
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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge.Execution {

    [TestClass]
    public class UoeExecutionDbExtractTableAndSequenceListTest {

        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeExecutionDbExtractTableAndSequenceListTest)));

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
        public void OeExecutionDbExtractTableAndSequenceListTest_Dump_no_db() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            using (var exec = new UoeExecutionDbExtractTableAndSequenceList(env)) {
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "ExecutionHandledExceptions");
                Assert.IsFalse(exec.DatabaseConnectionFailed, "DbConnectionFailed");
            }
            env.Dispose();
        }

        [TestMethod]
        public void OeExecutionDbExtractTableAndSequenceListTest_Wrong_db_connection() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            env.DatabaseConnections = UoeDatabaseConnection.GetConnectionStrings("-db random -db cool");
            using (var exec = new UoeExecutionDbExtractTableAndSequenceList(env)) {
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.IsTrue(exec.ExecutionHandledExceptions, "ExecutionHandledExceptions");
                Assert.IsTrue(exec.DatabaseConnectionFailed, "DbConnectionFailed");
            }
            env.Dispose();
        }

        [TestMethod]
        public void OeExecutionDbExtractTableAndSequenceListTest_Dump_db() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            var base1Db = new UoeDatabaseLocation(Path.Combine(TestFolder, "base1.db"));
            var base2Db = new UoeDatabaseLocation(Path.Combine(TestFolder, "base2.db"));

            if (!base1Db.Exists()) {
                var dfPath = Path.Combine(TestFolder, "base1.df");
                File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");
                using (var dbAdministrator = new UoeDatabaseAdministrator(env.DlcDirectoryPath)) {
                    dbAdministrator.CreateWithDf(base1Db, dfPath);
                    dbAdministrator.CreateWithDf(base2Db, dfPath);
                }
            }

            env.DatabaseConnections = new []{ UoeDatabaseConnection.NewSingleUserConnection(base1Db), UoeDatabaseConnection.NewSingleUserConnection(base2Db) };
            env.DatabaseAliases = new List<IUoeExecutionDatabaseAlias> {
                new UoeExecutionDatabaseAlias {
                    DatabaseLogicalName = "base1",
                    AliasLogicalName = "alias1"
                }
            };

            using (var exec = new UoeExecutionDbExtractTableAndSequenceList(env)) {
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "ExecutionHandledExceptions");
                Assert.IsFalse(exec.DatabaseConnectionFailed, "DbConnectionFailed");

                Assert.AreEqual("base1.sequence1,alias1.sequence1,base2.sequence1", string.Join(",", exec.Sequences), "sequences");
                Assert.AreEqual("base1.table1,alias1.table1,base1._Sequence,alias1._Sequence,base2.table1,base2._Sequence", string.Join(",", exec.TablesCrc.Keys), "tables");
            }
            env.Dispose();
        }

        private bool GetEnvExecution(out UoeExecutionEnv env) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                env = null;
                return false;
            }
            env = new UoeExecutionEnv {
                DlcDirectoryPath = dlcPath
            };
            return true;
        }

    }
}
