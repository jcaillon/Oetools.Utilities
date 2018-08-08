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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class OeExecutionCompileTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(OeExecutionCompileTest)));

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
        public void OeExecutionCompile_Expect_Exception() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                // nothing to compile exception
                Assert.ThrowsException<ExecutionParametersException>(() => exec.Start());
            }
            using (var exec = new OeExecutionCompile(env)) {
               exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile("doesnotexist.p")
                };
                // file does not exist exception
                Assert.ThrowsException<ExecutionParametersException>(() => exec.Start());
            }
        }
        
        [TestMethod]
        [DataRow(true, true, true, true, true, true)]
        [DataRow(false, false, false, false, false, false)]
        [DataRow(true, false, false, false, false, false)]
        [DataRow(false, true, false, false, false, false)]
        [DataRow(false, false, true, false, false, false)]
        [DataRow(false, false, false, true, false, false)]
        [DataRow(false, false, false, false, true, false)]
        [DataRow(false, false, false, false, false, true)]
        [DataRow(false, false, false, false, true, true)]
        public void OeExecutionCompile_Test_Ok(bool debugList, bool preproc, bool listing, bool xref, bool xmlxref, bool analyze) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "ok.p"), _pOk);
            File.WriteAllText(Path.Combine(TestFolder, "inc.i"), "");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "ok.p"))
                };
                exec.CompileWithDebugList = debugList;
                exec.CompileWithPreprocess = preproc;
                exec.CompileWithListing = listing;
                exec.CompileWithXref = xref;
                exec.CompileUseXmlXref = xmlxref;
                exec.CompileInAnalysisMode = analyze;
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(false, exec.ExecutionFailed, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "One file compiled");
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(Path.Combine(TestFolder, "ok.p"), compiledFile.SourceFilePath, "SourcePath");
                Assert.AreEqual(Path.Combine(TestFolder, "ok.p"), compiledFile.CompiledFilePath, "CompiledPath");
                Assert.AreEqual(null, compiledFile.CompilationErrors, "CompilationErrors");
                Assert.AreEqual(true, compiledFile.CompiledCorrectly, "CompiledCorrectly");
                Assert.AreEqual(true, analyze || compiledFile.RequiredFiles == null, "RequiredFiles");
                Assert.AreEqual(true, analyze || compiledFile.RequiredTables == null, "RequiredTables");
                Assert.AreEqual(true, File.Exists(compiledFile.CompilationRcodeFilePath), "R");
                
                Assert.AreEqual(debugList, File.Exists(compiledFile.CompilationDebugListFilePath), "dbg");
                Assert.AreEqual(preproc, File.Exists(compiledFile.CompilationPreprocessedFilePath), "preproc");
                Assert.AreEqual(listing, File.Exists(compiledFile.CompilationListingFilePath), "listing");
                Assert.AreEqual(xref || analyze, File.Exists(compiledFile.CompilationXrefFilePath), "xref");
                Assert.AreEqual(xref && xmlxref && !analyze, File.Exists(compiledFile.CompilationXmlXrefFilePath), "xrefxml");
                Assert.AreEqual(analyze, File.Exists(compiledFile.CompilationFileIdLogFilePath), "fileid");
            }
        }

        private string _pOk => @"
MESSAGE ""ok"". {inc.i}";
        
        private string _pWarning => @"
QUIT.
QUIT.";
        
        private string _pError => @"
zerferfger";
        
        private string _pWarningsAndError => @"
QUIT.
QUIT.
PROCEDURE zefezrf:
    RETURN.
    RETURN.
END PROCEDURE.

compilationerror";
        
        private string _class1Ok => @"
USING namespacerandom.*.
CLASS namespacerandom.Class1 INHERITS Class2:
END CLASS.";
        
        private string _class2Ok => @"
CLASS namespacerandom.Class2 ABSTRACT:
END CLASS.";
        
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