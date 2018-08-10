#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UtilsTest.cs) is part of Oetools.Utilities.Test.
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class OeExecutionDbExtractTableAndSequenceListTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(OeExecutionDbExtractTableAndSequenceListTest)));

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
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            using (var exec = new OeExecutionDbExtractTableAndSequenceList(env)) {
                exec.NeedDatabaseConnection = true;
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "ExecutionHandledExceptions");
                Assert.IsFalse(exec.DbConnectionFailed, "DbConnectionFailed");
            }
        }
        
        [TestMethod]
        public void OeExecutionDbExtractTableAndSequenceListTest_Dump_db() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            // create dummy database
            if (!File.Exists(Path.Combine(TestFolder, "dummy.db"))) {
                var dfPath = Path.Combine(TestFolder, "dummy.df");
                File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");
                TestHelper.CreateDatabaseFromDf(Path.Combine(TestFolder, "dummy.db"), Path.Combine(TestFolder, "dummy.df"));
                TestHelper.CreateDatabaseFromDf(Path.Combine(TestFolder, "base.db"), Path.Combine(TestFolder, "dummy.df"));
            }

            env.UseProgressCharacterMode = true;
            env.DatabaseConnectionString = $"{DatabaseOperator.GetMonoConnectionString(Path.Combine(TestFolder, "dummy.db"))} {DatabaseOperator.GetMonoConnectionString(Path.Combine(TestFolder, "base.db"))}";
            
            using (var exec = new OeExecutionDbExtractTableAndSequenceList(env)) {
                exec.NeedDatabaseConnection = true;
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "ExecutionHandledExceptions");
                Assert.IsFalse(exec.DbConnectionFailed, "DbConnectionFailed");
            }
        }

        private bool GetEnvExecution(out EnvExecution env) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                env = null;
                return false;
            }
            env = new EnvExecution {
                DlcDirectoryPath = dlcPath
            };
            return true;
        }

    }
}