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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class OeExportReaderTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(OeExportReaderTest)));

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
        [DataRow("1", "1|+", false)]
        [DataRow("1\"", "1\"|+", false)]
        [DataRow(@"
""f k"" 10 ""f k"" 20 ""long
very
l""""o""""ng
line
""
10 ""long
very
long
line
ending"" 30 ""last""

", @"""f k""|10|""f k""|20|""long
very
l""o""ng
line
""|+10|""long
very
long
line
ending""|30|""last""|+", false)]
        [DataRow(@"3 ""field""", @"3|""field""|+", false)]
        [DataRow(@"1 """" 2", @"1|""""|2|+", false)]
        [DataRow(@"""field"" 2", @"""field""|2|+", false)]
        [DataRow(@"3 ""field"" 2", @"3|""field""|2|+", false)]
        [DataRow(@"3 ""field""", @"3|""field""|+", false)]
        [DataRow(@"3    ""field""", "+", true)]
        [DataRow(@" ", "+", true)]
        public void ReadOpenedgeUnformattedExportFile_Test(string content, string expected, bool hasExceptions) {
            
            var sb = new StringBuilder();
            using (var reader = new OeExportReader(content)) {
                int curRecord = 0;
                while (reader.ReadNextRecordField()) {
                    sb.Append(reader.RecordValue);
                    sb.Append("|");
                    if (reader.RecordNumber > curRecord) {
                        curRecord = reader.RecordNumber;
                        sb.Append("+");
                    }
                }
            }
            
            Assert.AreEqual(expected, sb.ToString(), content);
            
            var path = Path.Combine(TestFolder, "data.d");
            File.WriteAllText(path, content, Encoding.UTF8);

            
            sb = new StringBuilder();
            using (var reader = new OeExportReader(path, Encoding.UTF8)) {
                int curRecord = 0;
                while (reader.ReadNextRecordField()) {
                    sb.Append(reader.RecordValue);
                    sb.Append("|");
                    if (reader.RecordNumber > curRecord) {
                        curRecord = reader.RecordNumber;
                        sb.Append("+");
                    }
                }
            }
            
            Assert.AreEqual(expected, sb.ToString(), content);
        }

        [TestMethod]
        public void ReturnProgressSessionDefaultPropath_Test() {
            
            var references = new HashSet<string>();
            
            using (var reader = new OeExportReader(@"C:\Users\jcaillon\Desktop\New folder (2)\sc20ldos.xrf", Encoding.UTF8)) {
                while (reader.ReadNextRecordField()) {
                    if (reader.RecordFieldNumber != 3) {
                        continue;
                    }
                    string foundRef = null;
                    switch (reader.RecordValue) {
                        // dynamic access
                        case "ACCESS":
                            // "file.p" "file.p" line ACCESS [DATA-MEMBER] random.table1 idx_1 WHOLE-INDEX
                            if (reader.ReadNextRecordField() && reader.RecordFieldNumber == 4) {
                                foundRef = reader.RecordValue;
                                if (foundRef.Equals("DATA-MEMBER") && reader.ReadNextRecordField() && reader.RecordFieldNumber == 5) {
                                    foundRef = reader.RecordValue;
                                }
                            }
                            break;
                        // dynamic access
                        case "CREATE":
                        case "DELETE":
                        case "UPDATE":
                        case "SEARCH":
                            // "file.p" "file.p" line SEARCH random.table1 idx_1 WHOLE-INDEX
                            if (reader.ReadNextRecordField() && reader.RecordFieldNumber == 4) {
                                foundRef = reader.RecordValue;
                            }
                            break;
                        // static reference
                        case "REFERENCE":
                            // "file.p" "file.p" line REFERENCE random.table1 
                            if (reader.ReadNextRecordField() && reader.RecordFieldNumber == 4) {
                                foundRef = reader.RecordValue;
                            }
                            break;
                        // static reference
                        case "NEW-SHR-WORKFILE":
                        case "NEW-SHR-WORKTABLE":
                        case "SHR-WORKFILE":
                        case "SHR-WORKTABLE":
                            // "file.p" "file.p" line SHR-WORKFILE WORKtable2 LIKE random.table1
                            if (reader.ReadNextRecordField() && reader.RecordFieldNumber == 4 &&
                                reader.ReadNextRecordField() && reader.RecordFieldNumber == 5 &&
                                reader.ReadNextRecordField() && reader.RecordFieldNumber == 6) {
                                foundRef = reader.RecordValue;
                            }
                            break;
                        default:
                            continue;
                    }

                    if (!string.IsNullOrEmpty(foundRef) && foundRef.IndexOf('.') > 0 && !references.Contains(foundRef)) {
                        // make sure it's actually a table or sequence
                        references.Add(foundRef);
                    }
                }
            }
        }

    }
}