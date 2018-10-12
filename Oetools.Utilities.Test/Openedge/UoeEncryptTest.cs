#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExportReaderTest.cs) is part of Oetools.Utilities.Test.
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
    public class UoeXcodeTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeXcodeTest)));

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
        public void Test() {
            foreach (var password in new List<string> { null, "pwd", "waytoolongbutitsok" }) {
                var xcode = new UoeEncryptor(password);

                var content = "this is a test!";
                var filePath = Path.Combine(TestFolder, "test.txt");
                var filePathEncoded = Path.Combine(TestFolder, "test.txt.encoded");
                var filePathDecoded = Path.Combine(TestFolder, "test.txt.decoded");
            
                File.WriteAllText(filePath, content, Encoding.Default);

                xcode.ConvertFile(filePath, true, filePathEncoded);
                xcode.ConvertFile(filePathEncoded, false, filePathDecoded);
            
                Assert.AreEqual(content, File.ReadAllText(filePathDecoded, Encoding.Default));

                Assert.IsTrue(xcode.IsFileEncrypted(filePathEncoded));
                Assert.IsFalse(xcode.IsFileEncrypted(filePathDecoded));
            }
        }
    }
}