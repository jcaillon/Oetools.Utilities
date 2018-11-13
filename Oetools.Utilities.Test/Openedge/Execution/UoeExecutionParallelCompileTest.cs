#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionParallelCompileTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class UoeExecutionParallelCompileTest : UoeExecutionCompileTest {
        
        private static string _testClassFolder;

        protected new static string TestClassFolder => _testClassFolder ?? (_testClassFolder = TestHelper.GetTestFolder(nameof(UoeExecutionParallelCompileTest)));

        [ClassInitialize]
        public new static void Init(TestContext context) {
            Cleanup();
            Directory.CreateDirectory(TestClassFolder);
        }

        [ClassCleanup]
        public new static void Cleanup() {
            // stop dummy database
            if (TestHelper.GetDlcPath(out string dlcPath)) {
                if (File.Exists(Path.Combine(TestClassFolder, "dummy.db"))) {
                    new UoeDatabaseOperator(dlcPath).Proshut(Path.Combine(TestClassFolder, "dummy.db"));
                }
            }
            if (Directory.Exists(TestClassFolder)) {
                Directory.Delete(TestClassFolder, true);
            }
        }
        
        protected override string TestFolder => TestClassFolder;
        
        protected override UoeExecutionCompile GetOeExecutionCompile(UoeExecutionEnv env) {
            return new UoeExecutionParallelCompile2(env);
        }

        public class UoeExecutionParallelCompile2 : UoeExecutionParallelCompile {
            
            public override int MinimumNumberOfFilesPerProcess => 1;

            public UoeExecutionParallelCompile2(AUoeExecutionEnv env) : base(env) { }
        }
        
        [TestMethod]
        public void OeExecutionParallelCompile_Test_Stop_compilation_on_error() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            for (int i = 1; i <= 15; i++) {
                File.WriteAllText(Path.Combine(TestFolder, $"test_stop_ok{i}.p"), @"QUIT.");
            }
            File.WriteAllText(Path.Combine(TestFolder, "test_stop_error.p"), @"derp.");
            env.UseProgressCharacterMode = true;
            using (var exec = GetOeExecutionCompile(env) as UoeExecutionParallelCompile) {
                Assert.IsNotNull(exec);
                exec.StopOnCompilationError = true;
                exec.MaxNumberOfProcesses = 20;
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok1.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok2.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok3.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok4.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok5.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok6.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok7.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok8.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok9.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok10.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok11.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok12.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_error.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok13.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok14.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_stop_ok15.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(1, exec.HandledExceptions.Count, $"should have just 1 stop exception {string.Join(",", exec.HandledExceptions)}");
                Assert.IsTrue(exec.HandledExceptions.Exists(e => e is UoeExecutionCompilationStoppedException), "stop exception");
            }
        }
        
        [TestMethod]
        public void OeExecutionParallelCompile_Test_Start_Failed() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            for (int i = 1; i <= 15; i++) {
                File.WriteAllText(Path.Combine(TestFolder, $"test_start_fail_proc{i}.p"), @"QUIT.");
            }
            env.UseProgressCharacterMode = true;
            using (var exec = GetOeExecutionCompile(env) as UoeExecutionParallelCompile) {
                Assert.IsNotNull(exec);
                exec.MaxNumberOfProcesses = 8;
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc1.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc2.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc3.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc_does_not_exist.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc4.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc5.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc6.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc7.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc8.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc9.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc10.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc11.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc12.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc13.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc14.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_start_fail_proc15.p"))
                };
                try {
                    exec.Start();
                    Assert.Fail("The start throws an exception");
                } catch (Exception e) {
                    Assert.IsNotNull(e);
                }
                exec.WaitForExecutionEnd();
                Assert.AreEqual(1, exec.HandledExceptions.Count, $"for the multi compilation, if a process fails to start we kill the others, so we should see a killed exception here {string.Join(",", exec.HandledExceptions)}");
            }
        }
        
        [TestMethod]
        public void OeExecutionParallelCompile_Test_NumberOfProcessesPerCore() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            for (int i = 1; i <= 15; i++) {
                File.WriteAllText(Path.Combine(TestFolder, $"test_nb_proc{i}.p"), @"QUIT.");
            }
            
            // when it goes ok
            env.UseProgressCharacterMode = true;
            using (var exec = GetOeExecutionCompile(env) as UoeExecutionParallelCompile) {
                Assert.IsNotNull(exec);
                exec.MaxNumberOfProcesses = 8;
                exec.FilesToCompile = new PathList<UoeFileToCompile>();
                for (int i = 1; i <= 15; i++) {
                    exec.FilesToCompile.Add(new UoeFileToCompile(Path.Combine(TestFolder, $"test_nb_proc{i}.p")));
                }
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(8, exec.TotalNumberOfProcesses);
                Assert.IsFalse(exec.ExecutionHandledExceptions, string.Join(",", exec.HandledExceptions));
            }
        }
    }
}