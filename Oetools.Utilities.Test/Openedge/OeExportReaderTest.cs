#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (OeExportReaderTest.cs) is part of Oetools.Utilities.Test.
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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Test.Openedge {
    
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

        private TestContext _testContextInstance;

        public TestContext TestContext {
            get => _testContextInstance;
            set => _testContextInstance = value;
        }

        [DataTestMethod]
        [DataRow(null, "", true, DisplayName = "null")]
        [DataRow("", "", true, DisplayName = "empty")]
        [DataRow("field", "<0.0>field", true, DisplayName = "1 field")]
        [DataRow("fi\"\"eld", "<0.0>fi\"eld", true, DisplayName = "weird case no space but 2 double quotes should read as 1 field")]
        [DataRow("\"field", "<0.0>field", true, DisplayName = "invalid but readable : 1 field with no ending quotes")]
        [DataRow("field1 \"field2\"\t\"fieldwith\"\"quotes\"\"3\"", "<0.0>field1<0.1>field2<0.2>fieldwith\"quotes\"3", true, DisplayName = "simple case")]
        [DataRow(" \t field1 \"field2\"  \t\"fieldwith\"\"quotes\"\"3\"  \t  ", "<0.0>field1<0.1>field2<0.2>fieldwith\"quotes\"3", true, DisplayName = "space don't matter")]
        [DataRow("field1 \"field2\" \"fieldwith\"quotes\"3\"", "<0.0>field1<0.1>field2<0.2>fieldwith\"quotes\"3", true, DisplayName = "not escaping quotes with double quotes but no space")]
        [DataRow("field1 \"field2\" \"fieldwith\"quotes field\"4\"", "<0.0>field1<0.1>field2<0.2>fieldwith\"quotes<0.3>field\"4", true, DisplayName = "invalid stuff : not escaping quotes with double quotes but with space!")]
        [DataRow("field1 \"field2\" \"field3 field4", "<0.0>field1<0.1>field2<0.2>field3 field4", true, DisplayName = "invalid stuff : field 3 no ending quote")]
        [DataRow("field1 \"field2\" \"field3\"\" field4", "<0.0>field1<0.1>field2<0.2>field3\" field4", true, DisplayName = "invalid stuff : field 3 no ending quote plus extra quote")]
        [DataRow("field1 \"field2\"\nfield1 \"field2\"", "<0.0>field1<0.1>field2<1.0>field1<1.1>field2", true, DisplayName = "several records")]
        [DataRow("field1 \"field2\"  \t\r\n   field1 \"field2\"  ", "<0.0>field1<0.1>field2<1.0>field1<1.1>field2", true, DisplayName = "several records with spaces")]
        [DataRow("field1 \"field2\rwith spaces\"\nfield1 \"field2\"", "<0.0>field1<0.1>field2\rwith spaces<1.0>field1<1.1>field2", true, DisplayName = "several records with multi lines records")]
        [DataRow("field1 \"field2\rwith spaces\"\nfield1 \"field2\"", "<0.0>field1<0.1>\"field2\rwith spaces\"<1.0>field1<1.1>\"field2\"", false, DisplayName = "several records with quotes")]
        [DataRow("\"field bouh", "<0.0>\"field bouh", false, DisplayName = "invalid but readable : 1 field with no ending quotes, keeping quotes")]
        public void Test_with_record_no_quotes(string input, string expected, bool noQuotes) {
            var sb = new StringBuilder();
            using (var reader = new OeExportReader(input)) {
                while (reader.MoveToNextRecordField()) {
                    sb.Append("<").Append(reader.RecordNumber).Append(".").Append(reader.RecordFieldNumber).Append(">").Append(noQuotes ? reader.RecordValueNoQuotes : reader.RecordValue);
                }
            }
            Assert.AreEqual(expected, sb.ToString(), "MoveToNextRecordField");

            sb.Clear();
            using (var reader = new OeExportReader(input)) {
                while (reader.ReadRecord(out List<string> record, out int recordNumber, noQuotes)) {
                    for (int i = 0; i < record.Count; i++) {
                        sb.Append("<").Append(recordNumber).Append(".").Append(i).Append(">").Append(record[i]);
                    }
                }
            }
            Assert.AreEqual(expected, sb.ToString(), "ReadRecord");
        }

    }
}