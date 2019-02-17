#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProcessArgsTest.cs) is part of Oetools.Utilities.Test.
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge {

    [TestClass]
    public class UoeProcessArgsTest {

        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeProcessArgsTest)));

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
        public void Append_WithPf() {
            var pf1 = Path.Combine(TestFolder, "1.pf");
            var pf2 = Path.Combine(TestFolder, "2.pf");
            var pf3 = Path.Combine(TestFolder, "3.pf");
            File.WriteAllText(pf1, "-pf " + pf2.ToQuotedArg());
            File.WriteAllText(pf2, "-db \"base1 ~\"allo~\"\"    -H hostname     -S 1024");
            File.WriteAllText(pf3, "-db base2     \n-H \"hos\"t\"nam\"e\n# ignore this line!\n    -S 1025 -pf 4.pf");

            var userConnectionString = "-pf " + pf1.ToQuotedArg() + " -pf " + pf3.ToQuotedArg();

            Assert.AreEqual("-db \"base1 \"\"allo\"\"\" -H hostname -S 1024 -db base2 -H hostname -S 1025 -pf 4.pf", new UoeProcessArgs().AppendFromQuotedArgs(userConnectionString).ToString());
        }

        [TestMethod]
        [DataRow(@"   -db test
# comment line
   -ld   logicalname #other comment
-H     localhost   -S portnumber", @"-db test -ld logicalname -H localhost -S portnumber")]
        [DataRow(@"", @"")]
        [DataRow(@"    ", @"")]
        public void AppendFromPfFilePath(string pfFileContent, string expected) {
            var pfPath = Path.Combine(TestFolder, "test.pf");
            File.WriteAllText(pfPath, pfFileContent);

            var args = new UoeProcessArgs().AppendFromPfFilePath(pfPath);
            if (File.Exists(pfPath)) {
                File.Delete(pfPath);
            }

            Assert.AreEqual(expected, args.ToString());
        }
    }
}
