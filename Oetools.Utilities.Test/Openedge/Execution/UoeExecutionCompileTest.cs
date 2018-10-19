#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionCompileTest.cs) is part of Oetools.Utilities.Test.
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class UoeExecutionCompileTest {
        
        private static string _testClassFolder;

        protected static string TestClassFolder => _testClassFolder ?? (_testClassFolder = TestHelper.GetTestFolder(nameof(UoeExecutionCompileTest)));
        
        [ClassInitialize]
        public static void Init(TestContext context) {
            Cleanup();
            Directory.CreateDirectory(TestClassFolder);
        }


        [ClassCleanup]
        public static void Cleanup() {
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
        
        protected virtual string TestFolder => TestClassFolder;
        
        protected void CreateDummyBaseIfNeeded() {
            // create dummy database
            if (TestHelper.GetDlcPath(out string dlcPath)) {
                if (!File.Exists(Path.Combine(TestFolder, "dummy.db"))) {
                    var dfPath = Path.Combine(TestFolder, "dummy.df");
                    File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING");
                    TestHelper.CreateDatabaseFromDf(Path.Combine(TestFolder, "dummy.db"), Path.Combine(TestFolder, "dummy.df"));
                }
                if (new UoeDatabaseOperator(dlcPath).GetBusyMode(Path.Combine(TestFolder, "dummy.db")) != DatabaseBusyMode.MultiUser) {
                    new UoeDatabaseOperator(dlcPath).ProServe(Path.Combine(TestFolder, "dummy.db"));
                }
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_Stop_compilation_on_error_or_warning() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "stop_compil_ok.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "stop_compil_warning.p"), @"QUIT. QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "stop_compil_error.p"), @"derp.");
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = true;

            // first scenario, we don't stop on error / warnings
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_warning.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_error.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_ok.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"not ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(1, exec.CompiledFiles.Count(cf => cf.CompiledCorrectly), "expect having compiled the last file even if the file[1] has errors");
                Assert.AreEqual(2, exec.CompiledFiles.ElementAt(1).CompilationErrors.Count, "expect to have compile the error file despite file[0] having warnings");
            }
            
            // second scenario, stop on error
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.StopOnCompilationError = true;
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_warning.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_error.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_ok.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(true, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(1, exec.HandledExceptions.Count, $"1 exception : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(typeof(UoeExecutionCompilationStoppedException), exec.HandledExceptions[0].GetType(), $"exeption type : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(0, exec.CompiledFiles.Count(cf => cf.CompiledCorrectly), "no files compiled correctly, we stopped before");
                Assert.AreEqual(1, exec.CompiledFiles.Count(cf => cf.CompiledWithWarnings), "1 file with warning");
                Assert.AreEqual(1, exec.CompiledFiles.ElementAt(0).CompilationErrors.Count, "get the errors on the file that were compiled");
                Assert.AreEqual(2, exec.CompiledFiles.ElementAt(1).CompilationErrors.Count, "also get errors on the file that made the compilation stopped");
            }
            
            // thirs scenario, stop on warning
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.StopOnCompilationWarning = true;
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_warning.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_error.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "stop_compil_ok.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(true, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(1, exec.HandledExceptions.Count, $"1 exception : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(typeof(UoeExecutionCompilationStoppedException), exec.HandledExceptions[0].GetType(), $"exeption type : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(0, exec.CompiledFiles.Count(cf => cf.CompiledCorrectly), "no files compiled correctly, we stopped before");
                Assert.AreEqual(1, exec.CompiledFiles.Count(cf => cf.CompiledWithWarnings), "1 file with warning");
                Assert.AreEqual(1, exec.CompiledFiles.ElementAt(0).CompilationErrors.Count, "get the errors on the file that were compiled");
                Assert.AreEqual(null, exec.CompiledFiles.ElementAt(1).CompilationErrors, "we don't have anything past the first file because we stopped the process");
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Expect_ExecutionParametersException() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            env.UseProgressCharacterMode = false;
            using (var exec = GetOeExecutionCompile(env)) {
                // nothing to compile exception
                Assert.ThrowsException<UoeExecutionParametersException>(() => exec.Start(), "nothing to compile");
            }
            using (var exec = GetOeExecutionCompile(env)) {
               exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile("doesnotexist.p")
                };
                // file does not exist exception
                Assert.ThrowsException<UoeExecutionParametersException>(() => exec.Start(), "a file does not exists");
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
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "ok.p"), @"MESSAGE ""ok"". {inc.i}");
            File.WriteAllText(Path.Combine(TestFolder, "inc.i"), "");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "ok.p"))
                };
                exec.CompileWithDebugList = debugList;
                exec.CompileWithPreprocess = preproc;
                exec.CompileWithListing = listing;
                exec.CompileWithXref = xref;
                exec.CompileUseXmlXref = xmlxref;
                exec.CompileInAnalysisMode = analyze;
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "One file compiled");
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(Path.Combine(TestFolder, "ok.p"), compiledFile.Path, "SourceFilePath");
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
        [DataRow(false)]
        [DataRow(true)]
        public void OeExecutionCompile_Test_both_error_handler(bool isProVersionHigherOrEqualTo102) {
            if (!TestHelper.GetDlcPath(out string _)) {
                return;
            }
            UoeExecutionEnv env = isProVersionHigherOrEqualTo102 ? new UoeExecutionEnv() : new EnvExecution2();
            
            Assert.AreEqual(isProVersionHigherOrEqualTo102, env.IsProVersionHigherOrEqualTo(new Version(10, 2)));
            
            // ERRORS ONLY
            
            File.WriteAllText(Path.Combine(TestFolder, "witherrors.p"), "\nzerferfger");
            File.WriteAllText(Path.Combine(TestFolder, "witherrors_in_include.p"), @"MESSAGE ""ok"". {error_include.i}");
            File.WriteAllText(Path.Combine(TestFolder, "error_include.i"), "\nzerferfger\nMESSAGE \"ok\".");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "witherrors.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "witherrors_in_include.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 2, "two files compiled");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(2, compiledFile.CompilationErrors.Count, "2 CompilationErrors");
                
                Assert.AreEqual(2, compiledFile.CompilationErrors[0].Line, "line 2");
                Assert.AreEqual(typeof(UoeCompilationError), compiledFile.CompilationErrors[0].GetType(), "level");
                Assert.AreEqual(201, compiledFile.CompilationErrors[0].ErrorNumber, "error 201");
                
                Assert.AreEqual(2, compiledFile.CompilationErrors[1].Line, "line 2");
                Assert.AreEqual(196, compiledFile.CompilationErrors[1].ErrorNumber, "error 196");
                
                // we do not have the same line error if we are using the version <10.2 and >=10.2
                
                compiledFile = exec.CompiledFiles.Last();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(3, compiledFile.CompilationErrors.Count, "3 CompilationErrors");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 2 : 3, compiledFile.CompilationErrors[0].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationError), compiledFile.CompilationErrors[0].GetType(), "level");
                Assert.AreEqual(247, compiledFile.CompilationErrors[0].ErrorNumber, "error 247");
                Assert.AreEqual(Path.Combine(TestFolder, "error_include.i"), compiledFile.CompilationErrors[0].FilePath, "source inc");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 2 : 3, compiledFile.CompilationErrors[1].Line, "line");
                Assert.AreEqual(198, compiledFile.CompilationErrors[1].ErrorNumber, "error 198");
                Assert.AreEqual(Path.Combine(TestFolder, "error_include.i"), compiledFile.CompilationErrors[1].FilePath, "source inc");
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
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "withwarnings_in_include.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "1 file compiled");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(false, compiledFile.CompiledWithWarnings, "not even compiled with warnings");
                Assert.AreEqual(5, compiledFile.CompilationErrors.Count, "5 CompilationErrors");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 3 : 1, compiledFile.CompilationErrors[0].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationWarning), compiledFile.CompilationErrors[0].GetType(), "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[0].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[0].FilePath, "source inc");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 6 : 1, compiledFile.CompilationErrors[1].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationWarning), compiledFile.CompilationErrors[1].GetType(), "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[1].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[1].FilePath, "source inc");
                
                Assert.AreEqual(1, compiledFile.CompilationErrors[2].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationWarning), compiledFile.CompilationErrors[2].GetType(), "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[2].ErrorNumber, "error");
                Assert.AreEqual(Path.Combine(TestFolder, "withwarnings_in_include.p"), compiledFile.CompilationErrors[2].FilePath, "source");
                
                Assert.AreEqual(1, compiledFile.CompilationErrors[3].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationError), compiledFile.CompilationErrors[3].GetType(), "level");
                Assert.AreEqual(201, compiledFile.CompilationErrors[3].ErrorNumber, "error");
                Assert.AreEqual(Path.Combine(TestFolder, "withwarnings_in_include.p"), compiledFile.CompilationErrors[3].FilePath, "source");
                
                Assert.AreEqual(1, compiledFile.CompilationErrors[4].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationError), compiledFile.CompilationErrors[4].GetType(), "level");
                Assert.AreEqual(196, compiledFile.CompilationErrors[4].ErrorNumber, "error");
                Assert.AreEqual(Path.Combine(TestFolder, "withwarnings_in_include.p"), compiledFile.CompilationErrors[4].FilePath, "source");
            }
            
            // ONLY WARNINGS
            
            File.WriteAllText(Path.Combine(TestFolder, "withwarnings_in_include.p"), @"MESSAGE ""ok"". {warnings_include.i}");
            
            env.UseProgressCharacterMode = true;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "withwarnings_in_include.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.IsTrue(exec.CompiledFiles != null && exec.CompiledFiles.Count == 1, "1 file compiled");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(true, compiledFile.CompiledWithWarnings, "but compiled with warnings");
                Assert.AreEqual(2, compiledFile.CompilationErrors.Count, "2 CompilationErrors");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 3 : 1, compiledFile.CompilationErrors[0].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationWarning), compiledFile.CompilationErrors[0].GetType(), "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[0].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[0].FilePath, "source inc");
                
                Assert.AreEqual(isProVersionHigherOrEqualTo102 ? 6 : 1, compiledFile.CompilationErrors[1].Line, "line");
                Assert.AreEqual(typeof(UoeCompilationWarning), compiledFile.CompilationErrors[1].GetType(), "level");
                Assert.AreEqual(15090, compiledFile.CompilationErrors[1].ErrorNumber, "error 201");
                Assert.AreEqual(Path.Combine(TestFolder, isProVersionHigherOrEqualTo102 ? "warnings_include.i" : "withwarnings_in_include.p"), compiledFile.CompilationErrors[1].FilePath, "source inc");
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_Compilation_progression() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "progression1.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "progression2.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "progression3.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "progression4.p"), @"QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "progression1.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "progression2.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "progression3.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "progression4.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"not ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(4, exec.NumberOfFilesTreated, "NumberOfFilesTreated");
            }
        }
        
        [TestMethod]
        [DataRow(@"MIN-SIZE = ?", false)]
        [DataRow(null, true)]
        [DataRow(@"", true)]
        [DataRow(@"MIN-SIZE = true", true)]
        public void OeExecutionCompile_Test_CompileStatementExtraOptions(string extraCompileStatementExtraOptions, bool executionSuccess) {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "extraoptions.p"), @"DEF VAR li_i AS INTEGER NO-UNDO. QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.CompileStatementExtraOptions = extraCompileStatementExtraOptions;
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "extraoptions.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(!executionSuccess, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(executionSuccess, exec.CompiledFiles.ElementAt(0).CompiledCorrectly);
            }
        }
        
        [TestMethod]
        [DataRow(null, true)]
        [DataRow(@"", true)]
        [DataRow(@"require-full-names,require-field-qualifiers", true)]
        [DataRow(@"require-full-names,require-field-qualifiers,require-full-keywords", false)]
        public void OeExecutionCompile_Test_Compile_using_compileoptions(string compilerOptions, bool compileSuccess) {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "compileoptions.p"), @"DEF VAR li_i AS INTEGER NO-UNDO. QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.CompileOptions = compilerOptions;
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "compileoptions.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(compileSuccess, exec.CompiledFiles.ElementAt(0).CompiledCorrectly);
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_SourcePath_versus_CompiledPath() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "sourcepath.p"), "zefzef. QUIT.");
            env.ProPathList = new List<string> { TestFolder };

            env.UseProgressCharacterMode = false;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "niceu.p")) {
                        CompiledPath = Path.Combine(TestFolder, "sourcepath.p")
                    }
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(false, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                Assert.AreEqual(2, compiledFile.CompilationErrors.Count, "2 CompilationErrors");
                
                Assert.AreEqual(Path.Combine(TestFolder, "niceu.p"), compiledFile.CompilationErrors[0].FilePath, "source");
                Assert.AreEqual(Path.Combine(TestFolder, "niceu.p"), compiledFile.CompilationErrors[1].FilePath, "source2");
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_PreferedTargetPath() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            var targetDir = Path.Combine(TestFolder, "target");
            Utils.CreateDirectoryIfNeeded(targetDir);
            
            File.WriteAllText(Path.Combine(TestFolder, "preferedtarget.p"), "QUIT.");
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "preferedtarget.p")) {
                        PreferedTargetDirectory = targetDir
                    }
                };
                exec.CompileWithDebugList = true;
                exec.CompileWithListing = true;
                exec.CompileWithPreprocess = true;
                exec.CompileWithXref = true;
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                
                var compiledFile = exec.CompiledFiles.First();
                Assert.AreEqual(true, compiledFile.CompiledCorrectly, "not CompiledCorrectly");
                
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + UoeConstants.ExtR), compiledFile.CompilationRcodeFilePath, "r");
                Assert.AreEqual(true, File.Exists(Path.Combine(targetDir, "preferedtarget" + UoeConstants.ExtR)), "r exists");
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + UoeConstants.ExtDebugList), compiledFile.CompilationDebugListFilePath);
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + UoeConstants.ExtListing), compiledFile.CompilationListingFilePath);
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + UoeConstants.ExtPreprocessed), compiledFile.CompilationPreprocessedFilePath);
                Assert.AreEqual(Path.Combine(targetDir, "preferedtarget" + UoeConstants.ExtXref), compiledFile.CompilationXrefFilePath);
            }
        }
        
        /// <summary>
        /// Cls files must be named like the class they define or they fail to compile
        /// </summary>
        [TestMethod]
        public void OeExecutionCompile_Test_Compile_class_Fails() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            File.WriteAllText(Path.Combine(TestFolder, "ClassFail.p"), @"
            USING namespace.random.*.
            CLASS namespace.random.Class1:
            END CLASS.");
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "ClassFail.p"))
                };
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(2, exec.CompiledFiles.ElementAt(0).CompilationErrors.Count, $"exec.CompiledFiles[0].CompilationErrors : {string.Join("\n", exec.CompiledFiles.ElementAt(0).CompilationErrors)}");
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
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
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
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "namespace", "random", "Class1.cls")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "namespace", "random", "Class2.cls"))
                };
                exec.CompileWithXref = true;
                exec.CompileWithPreprocess = true;
                exec.CompilerMultiCompile = multiCompile;
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
               
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), "all compile ok");
                
                Assert.AreEqual(true, File.Exists(exec.CompiledFiles.ElementAt(0).CompilationRcodeFilePath), "RCODE1");
                Assert.AreEqual(true, File.Exists(exec.CompiledFiles.ElementAt(1).CompilationRcodeFilePath), "RCODE2");
                Assert.AreEqual(exec.CompiledFiles.ElementAt(0).ClassNamespacePath, Path.Combine("namespace", "random"));
                Assert.AreEqual(exec.CompiledFiles.ElementAt(1).ClassNamespacePath, Path.Combine("namespace", "random"));
            }
        }
        
        [TestMethod]
        public void OeExecutionCompile_Test_Analysis_mode_referenced_files() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "namespace", "cool"));
            File.WriteAllText(Path.Combine(TestFolder, "namespace", "cool", "Class1.cls"), @"
            USING namespace.cool.*.
            CLASS namespace.cool.Class1 INHERITS Class2:
            END CLASS.");

            File.WriteAllText(Path.Combine(TestFolder, "namespace", "cool", "Class2.cls"), @"
            CLASS namespace.cool.Class2 ABSTRACT:
                METHOD PUBLIC VOID InitializeDate ():
                    {includes/analysefilesfirst.i}
                END METHOD.
            END CLASS.");
            
            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "includes"));
            File.WriteAllText(Path.Combine(TestFolder, "includes", "analysefilesfirst.i"), @"
            DEF VAR lc_ AS CHAR NO-UNDO.
            {analysefilessecond.i}");

            File.WriteAllText(Path.Combine(TestFolder, "analysefilessecond.i"), @"
            quit.
            ");        
            
            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = false;
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "namespace", "cool", "Class1.cls"))
                };
                exec.CompileInAnalysisMode = true;
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), "all compile ok");
                
                Assert.AreEqual(3, exec.CompiledFiles.ElementAt(0).RequiredFiles.Count, "required files");
                Assert.AreEqual(true, exec.CompiledFiles.ElementAt(0).RequiredFiles.ToList().Exists(f => f.Equals(Path.Combine(TestFolder, "includes", "analysefilesfirst.i"))), "1");
                Assert.AreEqual(true, exec.CompiledFiles.ElementAt(0).RequiredFiles.ToList().Exists(f => f.Equals(Path.Combine(TestFolder, "analysefilessecond.i"))), "1");
                Assert.AreEqual(true, exec.CompiledFiles.ElementAt(0).RequiredFiles.ToList().Exists(f => f.Equals(Path.Combine(TestFolder, "namespace", "cool", "Class2.cls"))), "1");
            }
        }
        
        [TestMethod]
        [DataRow(false, "MESSAGE STRING(CURRENT-VALUE(sequence1)).", "dummy.sequence1")]
        [DataRow(false, "MESSAGE STRING(CURRENT-VALUE(sequence1, dummy)).", "dummy.sequence1")]
        // Even if we use an alias of the database in the code, the reference is the logical name of the database behind the alias
        [DataRow(false, "MESSAGE STRING(CURRENT-VALUE(sequence1, alias1)).", "dummy.sequence1")]
        [DataRow(false, "ASSIGN CURRENT-VALUE(sequence1) = 1. FIND FIRST table1.", "dummy.sequence1,dummy.table1")]
        [DataRow(false, "FOR EACH table1 BY table1.field1:\nEND.", "dummy.table1")]
        [DataRow(false, "FOR EACH dummy.table1 BY dummy.table1.field1:\nEND.", "dummy.table1")]
        // However, when we qualify table with an alias name, we get the alias name is the reference... zzz
        [DataRow(false, "FOR EACH alias1.table1 BY alias1.table1.field1:\nEND.", "alias1.table1")]
        [DataRow(false, "CREATE table1.", "dummy.table1")]
        [DataRow(false, "FIND table1. ASSIGN table1.field1 = \"\".", "dummy.table1")]
        [DataRow(false, "DELETE table1.", "dummy.table1")]
        [DataRow(false, "DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.", "dummy.table1")]
        [DataRow(false, "DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1.", "dummy.table1")]
        [DataRow(false, "DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.", "dummy.table1")]
        [DataRow(false, "DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1.", "dummy.table1")]
        [DataRow(false, "DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "dummy.table1")]
        [DataRow(false, "DEFINE TEMP-TABLE tt1 LIKE table1.", "dummy.table1")]
        [DataRow(false, "DEFINE TEMP-TABLE tt1 LIKE alias1.table1.", "alias1.table1")]
        // in simplified mode, the tables referenced in "LIKE" statement do not appear
        // also, referenced sequences all appear as "dummy._Sequence"
        [DataRow(true, "MESSAGE STRING(CURRENT-VALUE(sequence1)).", "dummy._Sequence")]
        [DataRow(true, "MESSAGE STRING(CURRENT-VALUE(sequence1, dummy)).", "dummy._Sequence")]
        [DataRow(true, "MESSAGE STRING(CURRENT-VALUE(sequence1, alias1)).", "dummy._Sequence")]
        [DataRow(true, "ASSIGN CURRENT-VALUE(sequence1) = 1. FIND FIRST table1.", "dummy._Sequence,dummy.table1")]
        [DataRow(true, "FOR EACH table1 BY table1.field1:\nEND.", "dummy.table1")]
        [DataRow(true, "FOR EACH alias1.table1 BY alias1.table1.field1:\nEND.", "alias1.table1")]
        [DataRow(true, "FOR EACH dummy.table1 BY dummy.table1.field1:\nEND.", "dummy.table1")]
        [DataRow(true, "CREATE table1.", "dummy.table1")]
        [DataRow(true, "FIND table1. ASSIGN table1.field1 = \"\".", "dummy.table1")]
        [DataRow(true, "DELETE table1.", "dummy.table1")]
        [DataRow(true, "DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.", "")]
        [DataRow(true, "DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1.", "")]
        [DataRow(true, "DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.", "")]
        [DataRow(true, "DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1.", "")]
        [DataRow(true, "DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "")]
        [DataRow(true, "DEFINE TEMP-TABLE tt1 LIKE table1.", "")]
        public void OeExecutionCompile_Test_Analysis_mode_referenced_tables_or_sequences(bool analysisModeSimplifiedDatabaseReferences, string codeThatReferencesDatabase, string references) {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
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
            env.DatabaseConnectionString = UoeDatabaseOperator.GetMultiUserConnectionString(Path.Combine(TestFolder, "dummy.db"));
            env.DatabaseAliases = new List<IUoeExecutionDatabaseAlias> {
                new UoeExecutionDatabaseAlias {
                    DatabaseLogicalName = "dummy",
                    AliasLogicalName = "alias1"
                }
            };
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "analyserefdb.p"))
                };
                exec.CompileUseXmlXref = true;
                exec.CompileInAnalysisMode = true;
                exec.AnalysisModeSimplifiedDatabaseReferences = analysisModeSimplifiedDatabaseReferences;
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed procedure : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), $"all procedures compile ok : {string.Join(",", exec.CompiledFiles.ElementAt(0).CompilationErrors?.Select(e => e.Message) ?? new List<string>())}");
                
                Assert.AreEqual(references, string.Join(",", exec.CompiledFiles.ElementAt(0).RequiredDatabaseReferences.Select(r => r.QualifiedName)), "procedure ref");
                Assert.IsTrue(exec.CompiledFiles.ElementAt(0).RequiredDatabaseReferences.Where(r => r is UoeDatabaseReferenceTable).Cast<UoeDatabaseReferenceTable>().All(r => !string.IsNullOrEmpty(r.Crc)));
            }
        }
        
        [TestMethod]
        // Inherited class are not like include :
        // class1 inherits from class2
        // a reference to table is made in class2
        // if the only table changes (no change of code in class2), then only class 2 needs to be recompiled! 
        [DataRow(false, true, "MESSAGE STRING(CURRENT-VALUE(sequence1)).", "")]
        [DataRow(false, true, "FOR EACH alias1.table1 BY alias1.table1.field1:\nEND.", "")]
        [DataRow(false, true, "CREATE dummy.table1.", "")]
        [DataRow(false, true, "DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "")]
        [DataRow(false, false, "DEFINE WORKFILE wftable1 NO-UNDO LIKE dummy.table1.", "dummy.table1")]
        [DataRow(false, false, "DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "dummy.table1")]
        [DataRow(false, false, "DEFINE TEMP-TABLE tt1 LIKE alias1.table1.", "alias1.table1")]
        // in simplified mode, the tables referenced in "LIKE" statement do not appear
        // also, referenced sequences all appear as "dummy._Sequence"
        [DataRow(true, true, "MESSAGE STRING(CURRENT-VALUE(sequence1)).", "")]
        [DataRow(true, true, "FOR EACH alias1.table1 BY alias1.table1.field1:\nEND.", "")]
        [DataRow(true, true, "CREATE dummy.table1.", "")]
        [DataRow(true, true, "DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "")]
        [DataRow(true, false, "DEFINE WORKFILE wftable1 NO-UNDO LIKE dummy.table1.", "")]
        [DataRow(true, false, "DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.", "")]
        [DataRow(true, false, "DEFINE TEMP-TABLE tt1 LIKE alias1.table1.", "")]
        public void OeExecutionCompile_Test_Analysis_mode_referenced_tables_or_sequences_for_classes(bool analysisModeSimplifiedDatabaseReferences, bool inBaseClass, string codeThatReferencesDatabase, string references) {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            CreateDummyBaseIfNeeded();

            Utils.CreateDirectoryIfNeeded(Path.Combine(TestFolder, "namespace", "cool"));
            File.WriteAllText(Path.Combine(TestFolder, "namespace", "cool", "Class1.cls"), @"
            USING namespace.cool.*.
            CLASS namespace.cool.Class1 INHERITS Class2:
                " + (!inBaseClass ? codeThatReferencesDatabase : "") + @"
            END CLASS.");

            File.WriteAllText(Path.Combine(TestFolder, "namespace", "cool", "Class2.cls"), @"
            CLASS namespace.cool.Class2 ABSTRACT:
                METHOD PUBLIC VOID InitializeDate ():
                " + (inBaseClass ? codeThatReferencesDatabase : "") + @"
                END METHOD.
            END CLASS.");

            env.ProPathList = new List<string> { TestFolder };
            env.UseProgressCharacterMode = true;
            env.DatabaseConnectionString = UoeDatabaseOperator.GetMultiUserConnectionString(Path.Combine(TestFolder, "dummy.db"));
            env.DatabaseAliases = new List<IUoeExecutionDatabaseAlias> {
                new UoeExecutionDatabaseAlias {
                    DatabaseLogicalName = "dummy",
                    AliasLogicalName = "alias1"
                }
            };
            
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "namespace", "cool", "Class1.cls"))
                };
                exec.CompileUseXmlXref = true;
                exec.CompileInAnalysisMode = true;
                exec.AnalysisModeSimplifiedDatabaseReferences = analysisModeSimplifiedDatabaseReferences;
                exec.Start();
                exec.WaitForExecutionEnd();
                
                Assert.AreEqual(false, exec.ExecutionHandledExceptions, $"ExecutionFailed : {string.Join("\n", exec.HandledExceptions)}");
                Assert.AreEqual(true, exec.CompiledFiles.All(c => c.CompiledCorrectly), $"all compile ok : {string.Join(",", exec.CompiledFiles.ElementAt(0).CompilationErrors?.Select(e => e.Message) ?? new List<string>())}");
                
                Assert.AreEqual(references, string.Join(",", exec.CompiledFiles.ElementAt(0).RequiredDatabaseReferences.Select(r => r.QualifiedName)), "ref");
                Assert.IsTrue(exec.CompiledFiles.ElementAt(0).RequiredDatabaseReferences.Where(r => r is UoeDatabaseReferenceTable).Cast<UoeDatabaseReferenceTable>().All(r => !string.IsNullOrEmpty(r.Crc)));
            }
        }
        
        private int _iOeExecutionTestEvents;
        
        [TestMethod]
        public void OeExecutionCompile_Test_Events() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            
            File.WriteAllText(Path.Combine(TestFolder, "test_events1.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "test_events2.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "test_events3.p"), @"QUIT.");
            File.WriteAllText(Path.Combine(TestFolder, "test_events4.p"), @"QUIT.");
            
            // when it goes ok
            env.UseProgressCharacterMode = true;
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events1.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events2.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events3.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events4.p"))
                };
                exec.OnExecutionEnd += execution => _iOeExecutionTestEvents++;
                exec.OnExecutionOk += execution => _iOeExecutionTestEvents = _iOeExecutionTestEvents + 2;
                exec.OnExecutionException += execution => _iOeExecutionTestEvents = _iOeExecutionTestEvents + 4;
                _iOeExecutionTestEvents = 0;
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "ok");
                Assert.AreEqual(3, _iOeExecutionTestEvents);
                
            }
            
            // when it goes wrong
            env.UseProgressCharacterMode = true;
            env.ProExeCommandLineParameters = "oups i did it again";
            using (var exec = GetOeExecutionCompile(env)) {
                exec.FilesToCompile = new PathList<UoeFileToCompile> {
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events1.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events2.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events3.p")),
                    new UoeFileToCompile(Path.Combine(TestFolder, "test_events4.p"))
                };
                exec.OnExecutionEnd += execution => _iOeExecutionTestEvents++;
                exec.OnExecutionOk += execution => _iOeExecutionTestEvents = _iOeExecutionTestEvents + 2;
                exec.OnExecutionException += execution => _iOeExecutionTestEvents = _iOeExecutionTestEvents + 4;
                _iOeExecutionTestEvents = 0;
                exec.Start();
                exec.WaitForExecutionEnd();
                Assert.IsTrue(exec.ExecutionHandledExceptions, "errors");
                Assert.AreEqual(5, _iOeExecutionTestEvents);
            }
            
        }
        
        protected bool GetEnvExecution(out UoeExecutionEnv env) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                env = null;
                return false;
            }
            env = new UoeExecutionEnv {
                DlcDirectoryPath = dlcPath
            };
            return true;
        }

        protected virtual UoeExecutionCompile GetOeExecutionCompile(UoeExecutionEnv env) {
            return new UoeExecutionCompile(env);
        }

        private class EnvExecution2 : UoeExecutionEnv {
            public override bool IsProVersionHigherOrEqualTo(Version version) {
                return false;
            }
        }

    }
}