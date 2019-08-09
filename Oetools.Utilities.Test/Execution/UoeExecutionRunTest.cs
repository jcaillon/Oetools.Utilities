#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionRunTest.cs) is part of Oetools.Utilities.Test.
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
using DotUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Test.Execution {

    [TestClass]
    public class UoeExecutionRunTest {

        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeExecutionRunTest)));

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
        public void OeExecutionRun_Test_Expect_exception() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            using (var exec = new UoeExecutionRun(env, "")) {
                Assert.ThrowsException<UoeExecutionParametersException>(() => exec.ExecuteNoWait(), "nothing to run");
            }
            env.Dispose();
        }

        [TestMethod]
        public void OeExecutionRun_Test_Full_client_log() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            File.WriteAllText(Path.Combine(TestFolder, "test_run_full_client_log.p"), @"QUIT.");

            using (var exec = new UoeExecutionRun(env, Path.Combine(TestFolder, "test_run_full_client_log.p"))) {
                Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "log"));
                exec.RunSilently = true;
                exec.WorkingDirectory = Path.Combine(TestFolder, "log");
                exec.FullClientLogPath = "mylog.log";
                exec.ExecuteNoWait();
                exec.WaitForExit();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "no exceptions");
                Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "log", "mylog.log")));
            }

            using (var exec = new UoeExecutionRun(env, Path.Combine(TestFolder, "test_run_full_client_log.p"))) {
                exec.RunSilently = true;
                exec.FullClientLogPath = Path.Combine(TestFolder, "nice.log");
                exec.ExecuteNoWait();
                exec.WaitForExit();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "no exceptions");
                Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "nice.log")));
            }
            env.Dispose();
        }

        [TestMethod]
        public void OeExecutionRun_Test_LogEntryTypes() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            File.WriteAllText(Path.Combine(TestFolder, "test_run_full_client_log_error.p"), @"return error LOG-MANAGER:LOG-ENTRY-TYPES.");

            using (var exec = new UoeExecutionRun(env, Path.Combine(TestFolder, "test_run_full_client_log_error.p"))) {
                exec.RunSilently = true;
                exec.LogEntryTypes = "4GLMessages";
                exec.FullClientLogPath = Path.Combine(TestFolder, "error.log");
                exec.ExecuteNoWait();
                exec.WaitForExit();
                Assert.IsTrue(exec.ExecutionHandledExceptions, "has exception");
                Assert.AreEqual("4GLMessages", ((UoeExecutionOpenedgeException)exec.HandledExceptions[0]).ErrorMessage);
                Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "error.log")));
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
