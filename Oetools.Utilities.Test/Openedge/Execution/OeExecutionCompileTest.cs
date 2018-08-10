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

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge;
using Oetools.Utilities.Openedge.Database;
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
            CreateDummyBaseIfNeeded();
        }

        private static void CreateDummyBaseIfNeeded() {
            // create dummy database
            if (TestHelper.GetDlcPath(out string dlcPath)) {
                if (!File.Exists(Path.Combine(TestFolder, "dummy.db"))) {
                    var dfPath = Path.Combine(TestFolder, "dummy.df");
                    File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");
                    TestHelper.CreateDatabaseFromDf(Path.Combine(TestFolder, "dummy.db"), Path.Combine(TestFolder, "dummy.df"));
                }
                if (new DatabaseOperator(dlcPath).GetBusyMode(Path.Combine(TestFolder, "dummy.db")) != DatabaseBusyMode.MultiUser) {
                    new DatabaseOperator(dlcPath).ProServe(Path.Combine(TestFolder, "dummy.db"));
                }
            }
        }

        [ClassCleanup]
        public static void Cleanup() {
            // stop dummy database
            if (TestHelper.GetDlcPath(out string dlcPath)) {
                if (File.Exists(Path.Combine(TestFolder, "dummy.db"))) {
                    new DatabaseOperator(dlcPath).Proshut(Path.Combine(TestFolder, "dummy.db"));
                }
            }
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Expect_ExecutionParametersException() {
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
        
        /// <summary>
        /// Tests different combinations of options
        /// </summary>
        /// <param name="debugList"></param>
        /// <param name="preproc"></param>
        /// <param name="listing"></param>
        /// <param name="xref"></param>
        /// <param name="xmlxref"></param>
        /// <param name="analyze"></param>
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
            
            File.WriteAllText(Path.Combine(TestFolder, "ok.p"), @"MESSAGE ""ok"". {inc.i}");
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
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "One file compiled");
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(Path.Combine(TestFolder, "ok.p"), compiledFile.SourceFilePath, "SourcePath");
                Assert.AreEqual(Path.Combine(TestFolder, "ok.p"), compiledFile.CompiledFilePath, "CompiledPath");
                Assert.AreEqual(null, compiledFile.CompilationErrors, "CompilationErrors");
                Assert.AreEqual(true, compiledFile.CompiledCorrectly, "CompiledCorrectly");
                Assert.AreEqual(true, analyze || compiledFile.RequiredFiles == null, "RequiredFiles");
                Assert.AreEqual(true, analyze || compiledFile.RequiredDatabaseReferences == null, "RequiredTables");
                Assert.AreEqual(true, File.Exists(compiledFile.CompilationRcodeFilePath), "R");
                
                Assert.AreEqual(debugList, File.Exists(compiledFile.CompilationDebugListFilePath), "dbg");
                Assert.AreEqual(preproc, File.Exists(compiledFile.CompilationPreprocessedFilePath), "preproc");
                Assert.AreEqual(listing, File.Exists(compiledFile.CompilationListingFilePath), "listing");
                Assert.AreEqual(xref || analyze, File.Exists(compiledFile.CompilationXrefFilePath), "xref");
                Assert.AreEqual(xref && xmlxref && !analyze, File.Exists(compiledFile.CompilationXmlXrefFilePath), "xrefxml");
                Assert.AreEqual(analyze, File.Exists(compiledFile.CompilationFileIdLogFilePath), "fileid");
            }
        }
        
        /// <summary>
        /// Checks that we correctly get the errors/warning when compiling a file
        /// </summary>
        /// <remarks>
        /// there is a version for oe &lt; 10.2 and another for oe &gt;= 10.2
        /// in the older version we can only get the line at which the compiler failed
        /// if there are several errors, they will all appear on the same line, even if that's not true
        /// </remarks>
        /// <param name="isProVersionHigherOrEqualTo102"></param>
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void OeExecutionCompile_Test_both_error_handler(bool isProVersionHigherOrEqualTo102) {
            if (!TestHelper.GetDlcPath(out string _)) {
                return;
            }
            EnvExecution env = isProVersionHigherOrEqualTo102 ? new EnvExecution() : new EnvExecution2();
            
            Assert.AreEqual(isProVersionHigherOrEqualTo102, env.IsProVersionHigherOrEqualTo(new Version(10, 2)));
            
            // ERRORS ONLY
            
            File.WriteAllText(Path.Combine(TestFolder, "witherrors.p"), "\nzerferfger");
            File.WriteAllText(Path.Combine(TestFolder, "witherrors_in_include.p"), @"MESSAGE ""ok"". {error_include.i}");
            File.WriteAllText(Path.Combine(TestFolder, "error_include.i"), "\nzerferfger\nMESSAGE \"ok\".");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "witherrors.p")),
                    new FileToCompile(Path.Combine(TestFolder, "witherrors_in_include.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 2, "two files compiled");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(2, compiledFile.CompilationErrors.Count, "2 CompilationErrors");
                
                Assert.AreEqual(2, compiledFile.CompilationErrors[0].Line, "line 2");
                Assert.AreEqual(CompilationErrorLevel.Error, compiledFile.CompilationErrors[0].Level, "level");
                Assert.AreEqual(201, compiledFile.CompilationErrors[0].ErrorNumber, "error 201");
                
                Assert.AreEqual(2, compiledFile.CompilationErrors[1].Line, "line 2");
                Assert.AreEqual(196, compiledFile.CompilationErrors[1].ErrorNumber, "error 196");
                
                // we do not have the same line error if we are using the version <10.2 and >=10.2
                
                compiledFile = exec.CompiledFiles.Last();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(3, compiledFile.CompilationErrors.Count, "3 CompilationErrors");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 2 : 3, compiledFile.CompilationErrors[0].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Error, compiledFile.CompilationErrors[0].Level, "level");
                Assert.AreEqual(247, compiledFile.CompilationErrors[0].ErrorNumber, "error 247");
                Assert.AreEqual(Path.Combine(TestFolder, "error_include.i"), compiledFile.CompilationErrors[0].SourcePath, "source inc");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 2 : 3, compiledFile.CompilationErrors[1].Line, "line");
                Assert.AreEqual(198, compiledFile.CompilationErrors[1].ErrorNumber, "error 198");
                Assert.AreEqual(Path.Combine(TestFolder, "error_include.i"), compiledFile.CompilationErrors[1].SourcePath, "source inc");
            }
            
            // ERRORS AND WARNINGS
            
            File.WriteAllText(Path.Combine(TestFolder, "withwarnings_in_include.p"), @"MESSAGE ""ok"". {warnings_include.i} efezfe");
            File.WriteAllText(Path.Combine(TestFolder, "warnings_include.i"), @"
                QUIT.
                QUIT.
                PROCEDURE zefezrf:
                    RETURN.
                    RETURN.
                END PROCEDURE.");
            
            env.UseProgressCharacterMode = true;
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "withwarnings_in_include.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "1 file compiled");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(5, compiledFile.CompilationErrors.Count, "5 CompilationErrors");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 3 : 1, compiledFile.CompilationErrors[0].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Warning, compiledFile.CompilationErrors[0].Level, "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[0].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[0].SourcePath, "source inc");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 6 : 1, compiledFile.CompilationErrors[1].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Warning, compiledFile.CompilationErrors[1].Level, "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[1].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[1].SourcePath, "source inc");
                
                Assert.AreEqual(1, compiledFile.CompilationErrors[2].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Warning, compiledFile.CompilationErrors[2].Level, "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[2].ErrorNumber, "error");
                Assert.AreEqual(Path.Combine(TestFolder, "withwarnings_in_include.p"), compiledFile.CompilationErrors[2].SourcePath, "source");
                
                Assert.AreEqual(1, compiledFile.CompilationErrors[3].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Error, compiledFile.CompilationErrors[3].Level, "level");
                Assert.AreEqual(201, compiledFile.CompilationErrors[3].ErrorNumber, "error");
                Assert.AreEqual(Path.Combine(TestFolder, "withwarnings_in_include.p"), compiledFile.CompilationErrors[3].SourcePath, "source");
                
                Assert.AreEqual(1, compiledFile.CompilationErrors[4].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Error, compiledFile.CompilationErrors[4].Level, "level");
                Assert.AreEqual(196, compiledFile.CompilationErrors[4].ErrorNumber, "error");
                Assert.AreEqual(Path.Combine(TestFolder, "withwarnings_in_include.p"), compiledFile.CompilationErrors[4].SourcePath, "source");
            }
            
            // ONLY WARNINGS
            
            File.WriteAllText(Path.Combine(TestFolder, "withwarnings_in_include.p"), @"MESSAGE ""ok"". {warnings_include.i}");
            
            env.UseProgressCharacterMode = true;
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "withwarnings_in_include.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "1 file compiled");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(2, compiledFile.CompilationErrors.Count, "2 CompilationErrors");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 3 : 1, compiledFile.CompilationErrors[0].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Warning, compiledFile.CompilationErrors[0].Level, "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[0].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[0].SourcePath, "source inc");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 6 : 1, compiledFile.CompilationErrors[1].Line, "line");
                Assert.AreEqual(CompilationErrorLevel.Warning, compiledFile.CompilationErrors[1].Level, "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[1].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[1].SourcePath, "source inc");
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_Compilation_progression() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "progression.p"), @"QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "progression.p")),
                    new FileToCompile(Path.Combine(TestFolder, "progression.p")),
                    new FileToCompile(Path.Combine(TestFolder, "progression.p")),
                    new FileToCompile(Path.Combine(TestFolder, "progression.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"not ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(4, exec.NumberOfFilesTreated, "NumberOfFilesTreated");
            }
        }
        
        [TestMethod]
        [DataRow(null, true)]
        [DataRow(@"", true)]
        [DataRow(@"MIN-SIZE = true", true)]
        [DataRow(@"MIN-SIZE = ?", false)]
        public void OeExecutionCompile_Test_CompileStatementExtraOptions(string extraCompileStatementExtraOptions, bool executionSuccess) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "extraoptions.p"), @"DEF VAR li_i AS INTEGER NO-UNDO. QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.CompileStatementExtraOptions = extraCompileStatementExtraOptions;
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "extraoptions.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(!executionSuccess, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(executionSuccess, exec.CompiledFiles[0].CompiledCorrectly);
            }
        }
        
        [TestMethod]
        [DataRow(null, true)]
        [DataRow(@"", true)]
        [DataRow(@"require-full-names,require-field-qualifiers", true)]
        [DataRow(@"require-full-names,require-field-qualifiers,require-full-keywords", false)]
        public void OeExecutionCompile_Test_Compile_using_compileoptions(string compilerOptions, bool compileSuccess) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "compileoptions.p"), @"DEF VAR li_i AS INTEGER NO-UNDO. QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.CompileOptions = compilerOptions;
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "compileoptions.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(compileSuccess, exec.CompiledFiles[0].CompiledCorrectly);
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_SourcePath_versus_CompiledPath() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "sourcepath.p"), "zefzef. QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "niceu.p")) {
                        CompiledPath = Path.Combine(TestFolder, "sourcepath.p")
                    }
                };
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(2, compiledFile.CompilationErrors.Count, "2 CompilationErrors");
                
                Assert.AreEqual(Path.Combine(TestFolder, "niceu.p"), compiledFile.CompilationErrors[0].SourcePath, "source");
                Assert.AreEqual(Path.Combine(TestFolder, "niceu.p"), compiledFile.CompilationErrors[1].SourcePath, "source2");
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_PreferedTargetPath() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }

            var targetDir = Path.Combine(TestFolder, "target");
            Utils.CreateDirectoryIfNeeded(targetDir);
            
            File.WriteAllText(Path.Combine(TestFolder, "preferedtarget.p"), "QUIT.");
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "preferedtarget.p")) {
                        PreferedTargetDirectory = targetDir
                    }
                };
                exec.CompileWithDebugList = true;
                exec.CompileWithListing = true;
                exec.CompileWithPreprocess = true;
                exec.CompileWithXref = true;
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(true, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + OeConstants.ExtR), compiledFile.CompilationRcodeFilePath, "r");
                Assert.AreEqual(true, File.Exists(Path.Combine(targetDir, "preferedtarget" + OeConstants.ExtR)), "r exists");
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + OeConstants.ExtDebugList), compiledFile.CompilationDebugListFilePath);
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + OeConstants.ExtListing), compiledFile.CompilationListingFilePath);
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + OeConstants.ExtPreprocessed), compiledFile.CompilationPreprocessedFilePath);
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + OeConstants.ExtXref), compiledFile.CompilationXrefFilePath);
            }
        }
        
        /// <summary>
        /// Cls files must be named like the class they define or they fail to compile
        /// </summary>
        [TestMethod]
        public void OeExecutionCompile_Test_Compile_class_Faile() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }

            File.WriteAllText(Path.Combine(TestFolder, "ClassFail.p"), @"
            USING namespace.random.*.
            CLASS namespace.random.Class1:
            END CLASS.");
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "ClassFail.p"))
                };
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(2, exec.CompiledFiles[0].CompilationErrors.Count, $"exec.CompiledFiles[0].CompilationErrors : {string.Join("\n", exec.CompiledFiles[0].CompilationErrors)}");
            }
        }
        
        /// <summary>
        /// Tests the class compilation, it differs from .p compilation because for a single .cls compiled we can have
        /// several .r generated (if a class inherits or implements smthing, that smthing will be compiled as well)
        /// </summary>
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void OeExecutionCompile_Test_Compile_class(bool multiCompile) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }

            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "namespace", "random"));
            File.WriteAllText(Path.Combine(TestFolder, "namespace", "random", "Class1.cls"), @"
            USING namespace.random.*.
            CLASS namespace.random.Class1 INHERITS Class2:
            END CLASS.");

            File.WriteAllText(Path.Combine(TestFolder, "namespace", "random", "Class2.cls"), @"
            CLASS namespace.random.Class2 ABSTRACT:
            END CLASS.");
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "namespace", "random", "Class1.cls")),
                    new FileToCompile(Path.Combine(TestFolder, "namespace", "random", "Class2.cls"))
                };
                exec.CompileWithXref = true;
                exec.CompileWithPreprocess = true;
                exec.CompilerMultiCompile = multiCompile;
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
               
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), "all compile ok");
                
                Assert.AreEqual(true, File.Exists(exec.CompiledFiles[0].CompilationRcodeFilePath), "RCODE1");
                Assert.AreEqual(true, File.Exists(exec.CompiledFiles[1].CompilationRcodeFilePath), "RCODE2");
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_Analysis_mode_referenced_files() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            
            // write procedures and includes
            File.WriteAllText(Path.Combine(TestFolder, "analysefiles.p"), @"
            {includes/analysefilesfirst.i}            
            ");     

            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "includes"));
            File.WriteAllText(Path.Combine(TestFolder, "includes", "analysefilesfirst.i"), @"
            DEF VAR lc_ AS CHAR NO-UNDO.
            {analysefilessecond.i}");

            File.WriteAllText(Path.Combine(TestFolder, "analysefilessecond.i"), @"
            quit.
            ");        
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "analysefiles.p"))
                };
                exec.CompileInAnalysisMode = true;
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), "all compile ok");
                
                Assert.AreEqual(2, exec.CompiledFiles[0].RequiredFiles.Count, "required files");
                Assert.AreEqual(true, exec.CompiledFiles[0].RequiredFiles.Exists(f => f.Equals(Path.Combine(TestFolder, "includes", "analysefilesfirst.i"))), "1");
                Assert.AreEqual(true, exec.CompiledFiles[0].RequiredFiles.Exists(f => f.Equals(Path.Combine(TestFolder, "analysefilessecond.i"))), "1");
            }
        }
        
        [TestMethod]
        [DataRow("MESSAGE STRING(CURRENT-VALUE(sequence1)).", "dummy.sequence1", false)]
        [DataRow("ASSIGN CURRENT-VALUE(sequence1) = 1. FIND FIRST table1.", "dummy.sequence1,dummy.table1", false)]
        [DataRow("FOR EACH table1 BY table1.field1:\nEND.", "dummy.table1", false)]
        [DataRow("CREATE table1.", "dummy.table1", false)]
        [DataRow("FIND table1. ASSIGN table1.field1 = \"\".", "dummy.table1", false)]
        [DataRow("DELETE table1.", "dummy.table1", false)]
        [DataRow("DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.", "dummy.table1", false)]
        [DataRow("DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1.", "dummy.table1", false)]
        [DataRow("DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.", "dummy.table1", false)]
        [DataRow("DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1.", "dummy.table1", false)]
        [DataRow("DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "dummy.table1", false)]
        [DataRow("DEFINE TEMP-TABLE tt1 LIKE table1.", "dummy.table1", false)]
        // in simplified mode, the tables referenced in "LIKE" statement do not appear
        // also, referenced sequences all appear as "dummy._Sequence"
        [DataRow("MESSAGE STRING(CURRENT-VALUE(sequence1)).", "dummy._Sequence", true)]
        [DataRow("ASSIGN CURRENT-VALUE(sequence1) = 1. FIND FIRST table1.", "dummy._Sequence,dummy.table1", true)]
        [DataRow("FOR EACH table1 BY table1.field1:\nEND.", "dummy.table1", true)]
        [DataRow("CREATE table1.", "dummy.table1", true)]
        [DataRow("FIND table1. ASSIGN table1.field1 = \"\".", "dummy.table1", true)]
        [DataRow("DELETE table1.", "dummy.table1", true)]
        [DataRow("DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.", "", true)]
        [DataRow("DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1.", "", true)]
        [DataRow("DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.", "", true)]
        [DataRow("DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1.", "", true)]
        [DataRow("DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "", true)]
        [DataRow("DEFINE TEMP-TABLE tt1 LIKE table1.", "", true)]
        public void OeExecutionCompile_Test_Analysis_mode_referenced_tables_or_sequences(string codeThatReferencesDatabase, string references, bool analysisModeSimplifiedDatabaseReferences) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }

            CreateDummyBaseIfNeeded();

            // write procedures and includes
            File.WriteAllText(Path.Combine(TestFolder, "analyserefdb.p"), @"
        {includes/firstrefdb.i}            
        ");
            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "includes"));
            File.WriteAllText(Path.Combine(TestFolder, "includes", "firstrefdb.i"), @"
        {secondrefdb.i}");
            File.WriteAllText(Path.Combine(TestFolder, "secondrefdb.i"), codeThatReferencesDatabase);

            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = true;
            env.DatabaseConnectionString = DatabaseOperator.GetMultiConnectionString(Path.Combine(TestFolder, "dummy.db"));
            
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(Path.Combine(TestFolder, "analyserefdb.p"))
                };
                exec.CompileUseXmlXref = true;
                exec.CompileInAnalysisMode = true;
                exec.AnalysisModeSimplifiedDatabaseReferences = analysisModeSimplifiedDatabaseReferences;
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), $"all compile ok : {string.Join(",", exec.CompiledFiles[0].CompilationErrors?.Select(e => e.Message) ?? new List<string>())}");
                
                Assert.AreEqual(references, exec.CompiledFiles[0].RequiredDatabaseReferences != null ? string.Join(",", exec.CompiledFiles[0].RequiredDatabaseReferences) : "", "ref");
            }
        }
        
        [TestMethod]
        // in simplified mode and when compiling class file, we need to find the generated .r in the progress
        // execution to get the RCODE-INFO
        [DataRow("MESSAGE STRING(CURRENT-VALUE(sequence1)).", "dummy._Sequence", true, true)]
        [DataRow("ASSIGN CURRENT-VALUE(sequence1) = 1. FIND FIRST table1.", "dummy._Sequence,dummy.table1", true, true)]
        [DataRow("FOR EACH table1 BY table1.field1:\nEND.", "dummy.table1", true, true)]
        [DataRow("CREATE table1.", "dummy.table1", true, true)]
        [DataRow("FIND table1. ASSIGN table1.field1 = \"\".", "dummy.table1", true, true)]
        [DataRow("DELETE table1.", "dummy.table1", true, true)]
        [DataRow("DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "", true, true)]
        [DataRow("DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.", "", false, true)]
        [DataRow("DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1.", "", false, true)]
        [DataRow("DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.", "", false, true)]
        [DataRow("DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1.", "", false, true)]
        [DataRow("DEFINE TEMP-TABLE tt1 LIKE table1.", "", false, true)]
        public void OeExecutionCompile_Test_Analysis_mode_referenced_tables_or_sequences_for_classes(string codeThatReferencesDatabase, string references, bool inMethod, bool useClassFile) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }

            CreateDummyBaseIfNeeded();

            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "namespace", "cool"));
            File.WriteAllText(Path.Combine(TestFolder, "namespace", "cool", "Class1.cls"), @"
            USING namespace.cool.*.
            CLASS namespace.cool.Class1 INHERITS Class2:
            END CLASS.");

            File.WriteAllText(Path.Combine(TestFolder, "namespace", "cool", "Class2.cls"), @"
            CLASS namespace.cool.Class2 ABSTRACT:
                " + (inMethod ? "" : codeThatReferencesDatabase) + @"
                METHOD PUBLIC VOID InitializeDate ():
                " + (inMethod ? codeThatReferencesDatabase : "") + @"
                END METHOD.
            END CLASS.");

            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = true;
            env.DatabaseConnectionString = DatabaseOperator.GetMultiConnectionString(Path.Combine(TestFolder, "dummy.db"));
            
            using (var exec = new OeExecutionCompile(env)) {
                exec.FilesToCompile = new List<FileToCompile> {
                    new FileToCompile(useClassFile ? Path.Combine(TestFolder, "namespace", "cool", "Class1.cls") : Path.Combine(TestFolder, "analyserefdb.p"))
                };
                exec.CompileUseXmlXref = true;
                exec.CompileInAnalysisMode = true;
                exec.AnalysisModeSimplifiedDatabaseReferences = true;
                exec.Start();
                exec.WaitForProcessExit();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), $"all compile ok : {string.Join(",", exec.CompiledFiles[0].CompilationErrors?.Select(e => e.Message) ?? new List<string>())}");
                
                Assert.AreEqual(references, exec.CompiledFiles[0].RequiredDatabaseReferences != null ? string.Join(",", exec.CompiledFiles[0].RequiredDatabaseReferences) : "", "ref");
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

        private class EnvExecution2 : EnvExecution {
            public override bool IsProVersionHigherOrEqualTo(Version version) {
                return false;
            }
        }

    }
}