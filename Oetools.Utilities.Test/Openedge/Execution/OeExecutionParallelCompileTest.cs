#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (OeExecutionParallelCompileTest.cs) is part of Oetools.Utilities.Test.
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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class OeExecutionParallelCompileTest : OeExecutionCompileTest {
        
        private static string _testClassFolder;

        protected new static string TestClassFolder => _testClassFolder ?? (_testClassFolder = TestHelper.GetTestFolder(nameof(OeExecutionParallelCompileTest)));

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
                    new DatabaseOperator(dlcPath).Proshut(Path.Combine(TestClassFolder, "dummy.db"));
                }
            }
            if (Directory.Exists(TestClassFolder)) {
                Directory.Delete(TestClassFolder, true);
            }
        }
        
        protected override string TestFolder => TestClassFolder;
        
        protected override OeExecutionCompile GetOeExecutionCompile(EnvExecution env) {
            return new OeExecutionParallelCompile2(env);
        }

        public class OeExecutionParallelCompile2 : OeExecutionParallelCompile {
            
            public override int MinimumNumberOfFilesPerProcess => 1;

            public OeExecutionParallelCompile2(IEnvExecution env) : base(env) { }
        }
        
        [TestMethod]
        public void OeExecutionParallelCompile_Test_NumberOfProcessesPerCore() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "test_nb_proc.p"), @"QUIT.");
            
            // when it goes ok
            env.UseProgressCharacterMode = true;
            using (var exec = GetOeExecutionCompile(env) as OeExecutionParallelCompile) {
                Assert.IsNotNull(exec);
                exec.MaxNumberOfProcesses = 2;
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p")),
                    new FileToCompile(Path.Combine(TestFolder, "test_nb_proc.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(Environment.ProcessorCount * 2, exec.TotalNumberOfProcesses);
                Assert.IsFalse(exec.ExecutionHandledExceptions, string.Join(",", exec.HandledExceptions));
            }
        }
    }
}