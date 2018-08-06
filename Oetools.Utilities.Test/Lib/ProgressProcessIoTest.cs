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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class ProgressProcessIoTest {
        
        private static string _testFolder;
        private bool _hasExited1;
        private bool _hasExited2;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ProgressProcessIoTest)));

        /// <summary>
        /// char mode silent = Bread and butter as it works silently on both platform (win + linux)
        /// </summary>
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". PUT UNFORMATTED ""ok"".", "", 0, "okok", false)]
        [DataRow(@"compilation error!!", "", 2, null, true)]
        [DataRow(@"quit.", "-errorparameters", 2, null, true)]
        [DataRow(@"return error ""my error"".", "", 0, "", false)]
        [DataRow(@"
DEFINE VARIABLE lc_1 AS CHARACTER NO-UNDO.

DEFINE VARIABLE li_i AS INTEGER NO-UNDO.
DO li_i = 1 TO 33000:
    ASSIGN lc_1 = lc_1 + ""a"".
END.
        ", "", 0, null, true)] // run time error
        public void ProgressProcessIo_TestCharMode_Batch(string procContent, string parameters, int expectedExitCode, string expectedStandard, bool expectErrors) {
            StartProc(procContent, true, true, parameters, expectedExitCode, expectedStandard, expectErrors, null);
        }
        
        /// <summary>
        /// char mode not silent, will show a cmd
        /// </summary>
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". QUIT.", "", 0, "", false)]
        public void ProgressProcessIo_TestCharMode(string procContent, string parameters, int expectedExitCode, string expectedStandard, bool expectErrors) {
            StartProc(procContent, true, false, parameters, expectedExitCode, expectedStandard, expectErrors, null);
        }
        
        /// <summary>
        /// gui mode not silent, will show the ugly grey windows
        /// </summary>
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". QUIT.", "", 0, "", false)]
        public void ProgressProcessIo_TestGuiMode(string procContent, string parameters, int expectedExitCode, string expectedStandard, bool expectErrors) {
            StartProc(procContent, false, false, parameters, expectedExitCode, expectedStandard, expectErrors, null);
        }
        
        /// <summary>
        /// gui mode silent, should automatically hide the prowin executable from taskbar
        /// </summary>
        [TestMethod]
        [DataRow(@"IF SESSION:BATCH-MODE THEN PUT UNFORMATTED ""ok"".", "", 0, "ok", false)]
        public void ProgressProcessIo_TestGuiMode_Batch(string procContent, string parameters, int expectedExitCode, string expectedStandard, bool expectErrors) {
            StartProc(procContent, true, true, parameters, expectedExitCode, expectedStandard, expectErrors, null);
        }
        
        /// <summary>
        /// char mode silent, try the exit event
        /// </summary>
        [TestMethod]
        [DataRow(@"DISPLAY ""ok"". PUT UNFORMATTED ""_nice"".", "", 0, "ok_nice", false)]
        public void ProgressProcessIo_TestCharMode_Batch_ExitedEvent(string procContent, string parameters, int expectedExitCode, string expectedStandard, bool expectErrors) {
            _hasExited1 = false;
            StartProc(procContent, false, true, parameters, expectedExitCode, expectedStandard, expectErrors, (sender, args) => _hasExited1 = true);
            Assert.IsTrue(_hasExited1);
        }
        
        /// <summary>
        /// try to kill a running process(exit code = 1)
        /// </summary>
        /// <remarks>works if you kill the process manually but with process.kill() it returns -1</remarks>
        [TestMethod]
        [DataRow(true, @"PAUSE 10.", "", 1, "")]
        [DataRow(false, @"DISPLAY ""ok"".", "", 0, "ok")]
        public void ProgressProcessIo_TestCharMode_Batch_ExitedEvent_Async(bool kill, string procContent, string parameters, int expectedExitCode, string expectedStandard) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var proc = Path.Combine(TestFolder, "test.p");
            File.WriteAllText(proc, procContent);

            var process = new ProgressProcessIo(dlcPath, true, false) {
                WorkingDirectory = TestFolder
            };
            _hasExited2 = false;
            process.OnProcessExit += (sender, args) => _hasExited2 = true;

            process.ExecuteAsync($"-p test.p {parameters}");

            if (kill) {
                Task.Factory.StartNew(() => {
                    Thread.Sleep(1000);
                    process.Kill();
                });
            }
            
            process.WaitForExit();

            Assert.AreEqual(expectedExitCode, Math.Abs(process.ExitCode), $"{process.ExecutablePath} {process.StartParameters}");
            Assert.AreEqual(expectedStandard, process.BatchModeOutput.ToString());
            Assert.IsTrue(_hasExited2);
            Assert.AreEqual(kill, process.Killed);
        
            process.Dispose();
        }
        
        private void StartProc(string procContent, bool useCharMode, bool silent, string parameters, int expectedExitCode, string expectedStandard, bool expectErrors, EventHandler<EventArgs> exitedHandler) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var proc = Path.Combine(TestFolder, "test.p");
            File.WriteAllText(proc, procContent);

            var process = new ProgressProcessIo(dlcPath, useCharMode, true) {
                WorkingDirectory = TestFolder
            };
            if (exitedHandler != null) {
                process.OnProcessExit += exitedHandler;
            }
            process.Execute($"-p test.p {parameters}", silent);
            
            Assert.AreEqual(expectedExitCode, process.ExitCode, $"{process.ExecutablePath} {process.StartParameters}");
            if (expectedStandard != null) {
                Assert.AreEqual(expectedStandard, process.BatchModeOutput.ToString());
            }

            if (expectErrors) {
                Debug.WriteLine(process.BatchModeOutput);
                Assert.IsTrue(process.BatchModeOutput.Length > 1, process.BatchModeOutput.Length.ToString());
            }
        }

    }
}