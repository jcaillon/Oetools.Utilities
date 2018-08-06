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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Builder.Core2.Execution;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class ProgressProcessIoTest {
        
        private static string _testFolder;
        private bool _hasExited1;
        private bool _hasExited2;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ProgressProcessIoTest)));

        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". QUIT.", "", 0, "", "")] // char mode not silent, will show a cmd
        public void ProgressProcessIo_TestCharMode(string procContent, string parameters, int expectedExitCode, string expectedStandard, string expectedError) {
            StartProc(procContent, true, false, parameters, expectedExitCode, expectedStandard, expectedError, null);
        }
        
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". QUIT.", "", 0, "", "")] // gui mode not silent, will show the ugly grey windows
        public void ProgressProcessIo_TestGuiMode(string procContent, string parameters, int expectedExitCode, string expectedStandard, string expectedError) {
            StartProc(procContent, false, false, parameters, expectedExitCode, expectedStandard, expectedError, null);
        }
        
        [TestMethod]
        [DataRow(@"PAUSE 1. DISPLAY ""ok"". QUIT.", "", 0, "ok", "")] // gui mode silent, should automatically hide the prowin executable from taskbar
        public void ProgressProcessIo_TestGuiMode_Batch(string procContent, string parameters, int expectedExitCode, string expectedStandard, string expectedError) {
            StartProc(procContent, false, true, parameters, expectedExitCode, expectedStandard, expectedError, null);
        }
        
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"".", "", 0, "", "")] // char mode not silent, will show a cmd
        public void ProgressProcessIo_TestCharMode_Batch_ExitedEvent(string procContent, string parameters, int expectedExitCode, string expectedStandard, string expectedError) {
            _hasExited1 = false;
            StartProc(procContent, false, true, parameters, expectedExitCode, expectedStandard, expectedError, (sender, args) => _hasExited1 = true);
            Assert.IsTrue(_hasExited1);
        }
        
        [TestMethod]
        [DataRow(true, @"PAUSE 5.", "", 1, "", "")] // kill exit code 1
        [DataRow(false, @"DISPLAY ""ok"".", "", 0, "ok", "")]
        public void ProgressProcessIo_TestCharMode_Batch_ExitedEvent_Async(bool kill, string procContent, string parameters, int expectedExitCode, string expectedStandard, string expectedError) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var proc = Path.Combine(TestFolder, "test.p");
            File.WriteAllText(proc, procContent);

            using (var process = new ProgressProcessIo(dlcPath, true, false) {
                WorkingDirectory = TestFolder
            }) {
                _hasExited2 = false;
                process.OnProcessExit += (sender, args) => _hasExited2 = true;

                process.ExecuteAsync($"-p test.p {parameters}");

                if (kill) {
                    process.Kill();
                }
                
                process.WaitForExit();

                Assert.AreEqual(expectedExitCode, process.ExitCode, $"{process.Executable} {process.StartParameters}");
                Assert.AreEqual(expectedStandard, process.StandardOutput.ToString());
                Assert.AreEqual(expectedError, process.ErrorOutput.ToString());
                Assert.IsTrue(_hasExited2);
                if (kill) {
                    Assert.IsTrue(process.Killed);
                }
            }
        }
        
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". QUIT.", "", 0, "ok", "")]
        public void ProgressProcessIo_TestCharMode_Batch(string procContent, string parameters, int expectedExitCode, string expectedStandard, string expectedError) {
            StartProc(procContent, true, true, parameters, expectedExitCode, expectedStandard, expectedError, null);
        }

        
        private void StartProc(string procContent, bool useCharMode, bool silent, string parameters, int expectedExitCode, string expectedStandard, string expectedError, EventHandler<EventArgs> exitedHandler) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var proc = Path.Combine(TestFolder, "test.p");
            File.WriteAllText(proc, procContent);

            var process = new ProgressProcessIo(dlcPath, useCharMode, false) {
                WorkingDirectory = TestFolder
            };
            if (exitedHandler != null) {
                process.OnProcessExit += exitedHandler;
            }
            process.Execute($"-p test.p {parameters}", silent);
            
            Assert.AreEqual(expectedExitCode, process.ExitCode, $"{process.Executable} {process.StartParameters}");
            Assert.AreEqual(expectedStandard, process.StandardOutput.ToString());
            Assert.AreEqual(expectedError, process.ErrorOutput.ToString());
        }

    }
}