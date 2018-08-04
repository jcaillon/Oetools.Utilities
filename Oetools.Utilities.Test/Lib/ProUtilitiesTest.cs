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
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class ProUtilitiesTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ProUtilitiesTest)));

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
        public void GetProPathFromIniFile_TestEnvVarReplacement() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }
            
            var iniPath = Path.Combine(TestFolder, "test.ini");
            File.WriteAllText(iniPath, "[Startup]\nPROPATH=t:\\error:exception\";C:\\Windows,%TEMP%;z:\\nooooop");

            var list = ProUtilities.GetProPathFromIniFile(iniPath, TestFolder);

            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(list.ToList().Exists(s => s.Equals("C:\\Windows")));
            Assert.IsTrue(list.ToList().Exists(s => s.Equals(Environment.GetEnvironmentVariable("TEMP"))));
        }
        
        [TestMethod]
        [DataRow(@"OpenEdge Release 11.7 as of Mon Mar 27 10:21:54 EDT 2017", 11, 7, 0)]
        [DataRow(@"OpenEdge Release 9.1D05 as of Mon Mar 27 10:21:54 EDT 2017", 9, 1, 5)]
        [DataRow(@"OpenEdge Release 10.2B0801 as of Mon Mar 27 10:21:54 EDT 2017", 10, 2, 801)]
        [DataRow(@"OpenEdge Release 11.1.2.001 as of Mon Mar 27 10:21:54 EDT 2017", 11, 1, 2)]
        [DataRow(@"OpenEdge Release 11.1.2 as of Mon Mar 27 10:21:54 EDT 2017", 11, 1, 2)]
        public void GetProVersionFromDlc_Test(string version, int major, int minor, int patch) {
            var versionFilePath = Path.Combine(TestFolder, "version");
            File.WriteAllText(versionFilePath, version);
            Assert.AreEqual(new Version(major, minor, patch), ProUtilities.GetProVersionFromDlc(TestFolder));
        }
        
        [TestMethod]
        public void GetProExecutableFromDlc_Test() {
            if (string.IsNullOrEmpty(ProUtilities.GetDlcPath())) {
                return;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Assert.IsTrue(ProUtilities.GetProExecutableFromDlc(ProUtilities.GetDlcPath()).StartsWith(Path.Combine(ProUtilities.GetDlcPath(), "bin", "prowin")));
            }
            Assert.AreEqual(Path.Combine(ProUtilities.GetDlcPath(), "bin", "_progres.exe"), ProUtilities.GetProExecutableFromDlc(ProUtilities.GetDlcPath(), true));
        }
        
        [TestMethod]
        public void ListProlibFilesInDirectory_Test() {
            var prolib = Path.Combine(TestFolder, "prolib");
            Directory.CreateDirectory(prolib);
            Directory.CreateDirectory(Path.Combine(prolib, "sub"));
            File.WriteAllText(Path.Combine(prolib, "1"), "");
            File.WriteAllText(Path.Combine(prolib, "2.pl"), "");
            File.WriteAllText(Path.Combine(prolib, "3"), "");
            File.WriteAllText(Path.Combine(prolib, "4.pl"), "");
            File.WriteAllText(Path.Combine(prolib, "sub", "5.pl"), "");

            var plList = ProUtilities.ListProlibFilesInDirectory(prolib);
            
            Assert.AreEqual(2, plList.Count);
            Assert.IsTrue(plList.Exists(s => Path.GetFileName(s).Equals("2.pl")));
            Assert.IsTrue(plList.Exists(s => Path.GetFileName(s).Equals("4.pl")));
        }
        
        [TestMethod]
        [DataRow(@"   -db test
# comment line  
   -ld   logicalname #other comment
-H     localhost   -S portnumber", @"-db test -ld logicalname -H localhost -S portnumber")]
        [DataRow(@"", @"")]
        [DataRow(@"    ", @"")]
        public void GetConnectionStringFromPfFile_IsOk(string pfFileContent, string expected) {
            var pfPath = Path.Combine(TestFolder, "test.pf");
            File.WriteAllText(pfPath, pfFileContent);

            var connectionString = ProUtilities.GetConnectionStringFromPfFile(pfPath);
            if (File.Exists(pfPath)) {
                File.Delete(pfPath);
            }

            Assert.AreEqual(expected, connectionString);
        }

    }
}