#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeUtilitiesTest.cs) is part of Oetools.Utilities.Test.
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
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Test.Openedge {
    
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
        public void ReturnProgressSessionDefaultPropath_Test() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var output = UoeUtilities.GetProgressSessionDefaultPropath(dlcPath, true);
            Assert.IsTrue(output.Exists(p => p.Contains("tty")));
            
            output = UoeUtilities.GetProgressSessionDefaultPropath(dlcPath, false);
            Assert.IsTrue(output.Exists(p => p.Contains("gui")));

        }
        
        [TestMethod]
        [DataRow(49)] // This Technical Support Knowled...
        [DataRow(1)] // The -nb parameter is followed by a number that sp...
        [DataRow(1964)] // COBOL binary or COMP var...
        [DataRow(612)] // PROGRESS tried to read or write the
        public void GetOpenedgeErrorDetailedMessage_Test(int errorNumber) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var res = UoeUtilities.GetOpenedgeProMessage(dlcPath, errorNumber);
            Debug.WriteLine(res);
            Assert.IsNotNull(res, "null");
        }
        
        [TestMethod]
        public void GetProPathFromIniFile_TestEnvVarReplacement() {
           
            var iniPath = Path.Combine(TestFolder, "test.ini");
            File.WriteAllText(iniPath, "[Startup]\nPROPATH=t:\\error:exception\";C:\\Windows,%TEMP%;z:\\nooooop");

            var list = UoeUtilities.GetProPathFromIniFile(iniPath, TestFolder);

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
            Assert.AreEqual(new Version(major, minor, patch), UoeUtilities.GetProVersionFromDlc(TestFolder));
        }
        
        [TestMethod]
        public void GetProExecutableFromDlc_Test() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            Assert.IsTrue(UoeUtilities.GetProExecutableFromDlc(UoeUtilities.GetDlcPathFromEnv()).StartsWith(Path.Combine(dlcPath, "bin", "prowin")));
            Assert.AreEqual(Path.Combine(UoeUtilities.GetDlcPathFromEnv(), "bin", "_progres.exe"), UoeUtilities.GetProExecutableFromDlc(dlcPath, true));
            Assert.ThrowsException<UoeExecutionParametersException>(() => UoeUtilities.GetProExecutableFromDlc(TestFolder, true));
            Assert.ThrowsException<UoeExecutionParametersException>(() => UoeUtilities.GetProExecutableFromDlc(TestFolder));
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

            var plList = UoeUtilities.ListProlibFilesInDirectory(prolib);
            
            Assert.AreEqual(2, plList.Count);
            Assert.IsTrue(plList.Exists(s => Path.GetFileName(s ?? "").Equals("2.pl")));
            Assert.IsTrue(plList.Exists(s => Path.GetFileName(s ?? "").Equals("4.pl")));
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

            var connectionString = UoeUtilities.GetConnectionStringFromPfFile(pfPath);
            if (File.Exists(pfPath)) {
                File.Delete(pfPath);
            }

            Assert.AreEqual(expected, connectionString);
        }
        
        [TestMethod]
        [DataRow(@"
""C:\folder space\file.p"" ""C:\folder space\file.p"" 4 ACCESS random.sequence1 SEQUENCE
/*MESSAGE STRING(CURRENT-VALUE(sequence1)).*/
""C:\folder space\file.p"" ""C:\folder space\file.p"" 7 UPDATE random.sequence2 SEQUENCE
/*ASSIGN CURRENT-VALUE(sequence1) = 1.*/
 ""C:\folder space\file.p"" ""C:\folder space\file.p"" 10 SEARCH random.table1 idx_1 WHOLE-INDEX 
/*FIND FIRST table1.*/
""C:\folder space\file.p"" ""C:\folder space\file.p"" 16 ACCESS random.table2 field1 
""C:\folder space\file.p"" ""C:\folder space\file.p"" 16 SEARCH random.table3 idx_1 WHOLE-INDEX
/*FOR EACH table1 BY table1.field1:*/
/*END.*/
 ""C:\folder space\file.p"" ""C:\folder space\file.p"" 20 CREATE random.table4  
/*CREATE table1.*/
""C:\folder space\file.p"" ""C:\folder space\file.p"" 24 ACCESS DATA-MEMBER random.table5 field1 
""C:\folder space\file.p"" ""C:\folder space\file.p"" 24 UPDATE random.table6 field1
/*ASSIGN table1.field1 = """".*/
""C:\folder space\file.p"" ""C:\folder space\file.p"" 27 DELETE random.table7 
/*DELETE table1.*/
""C:\folder space\file.p"" ""C:\folder space\file.p"" 32 REFERENCE random.table8 
""C:\folder space\file.p"" ""C:\folder space\file.p"" 32 NEW-SHR-WORKFILE WORKtable1 LIKE random.table9
/*DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.*/
/*DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1.  same thing as WORKFILE */
""C:\folder space\file.p"" ""C:\folder space\file.p"" 38 REFERENCE random.table10 
""C:\folder space\file.p"" ""C:\folder space\file.p"" 38 SHR-WORKFILE WORKtable LIKE random.table11
/*DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.*/
/*DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1.  same thing as WORKFILE */
""C:\folder space\file.p"" ""C:\folder space\file.p"" 42 REFERENCE random.table12 field1 
/*DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.*/
""C:\folder space\file.p"" ""C:\folder space\file.p"" 45 REFERENCE random.table13 
/*DEFINE TEMP-TABLE tt1 LIKE table1.*/
", @"random.sequence1,random.sequence2,random.table1,random.table2,random.table3,random.table4,random.table5,random.table6,random.table7,random.table8,random.table9,random.table10,random.table11,random.table12,random.table13")]
        public void GetDatabaseReferencesFromXrefFile_Test(string fileContent, string expected) {
            File.WriteAllText(Path.Combine(TestFolder, "test.xrf"), fileContent, Encoding.Default);
            var l = UoeUtilities.GetDatabaseReferencesFromXrefFile(Path.Combine(TestFolder, "test.xrf"), Encoding.Default);
            Assert.AreEqual(expected, string.Join(",", l));
        }
        
        [TestMethod]
        [DataRow(@"
[18/08/12@13:28:48.082+0200] P-005932 T-006876 1 4GL -- Logging level set to = 2
[18/08/12@13:28:48.082+0200] P-005932 T-006876 1 4GL -- No entry types are activated
[18/08/12@13:28:48.082+0200] P-005932 T-006876 1 4GL -- Logging level set to = 3
[18/08/12@13:28:48.082+0200] P-005932 T-006876 1 4GL -- Log entry types activated: FileID
[18/08/12@13:28:48.082+0200] P-005932 T-006876 2 4GL FILEID         Open
[18/08/12@13:28:48.082+0200] P-005932 T-006876 2 4GL FILEID         Open ID=9
[18/08/12@13:28:48.082+0200] P-005932 T-006876 2 4GL FILEID         Open C:\Users\Julien\analysefiles.xrf ID=9
[18/08/12@13:28:48.083+0200] P-005932 T-006876 2 4GL FILEID         Close C:\Users\Julien\analysefiles.xrf ID=9
[18/08/12@13:28:48.083+0200] P-005932 T-006876 2 4GL FILEID         Open C:\Work\Tests\UoeExecutionCompileTest\analysefiles.p ID=10
[18/08/12@13:28:48.085+0200] P-005932 T-006876 2 4GL FILEID         Open C:\Work\Tests\UoeExecutionCompileTest\includes with spaces\analysefilesfirst.i ID=11
[18/08/12@13:28:48.085+0200] P-005932 T-006876 2 4GL FILEID         Open C:\Work\Tests\UoeExecutionCompileTest\analysefilessecond.i ID=12
[18/08/12@13:28:48.085+0200] P-005932 T-006876 2 4GL FILEID         Close C:\Work\Tests\UoeExecutionCompileTest\analysefilessecond.i ID=12
[18/08/12@13:28:48.085+0200] P-005932 T-006876 2 4GL FILEID         Close C:\Work\Tests\UoeExecutionCompileTest\includes with spaces\analysefilesfirst.i ID=11
[18/08/12@13:28:48.086+0200] P-005932 T-006876 2 4GL FILEID         Close C:\Work\Tests\UoeExecutionCompileTest\analysefiles.p ID=10
[18/08/12@13:28:48.087+0200] P-005932 T-006876 2 4GL FILEID         Open C:\Users\Julien\analysefiles.r ID=9
[18/08/12@13:28:48.088+0200] P-005932 T-006876 2 4GL FILEID         Close C:\Users\Julien\analysefiles.r ID=9
[18/08/12@13:28:48.088+0200] P-005932 T-006876 1 4GL ----------     Log file closed at user's request
", @"C:\Users\Julien\analysefiles.xrf,C:\Work\Tests\UoeExecutionCompileTest\analysefiles.p,C:\Work\Tests\UoeExecutionCompileTest\includes with spaces\analysefilesfirst.i,C:\Work\Tests\UoeExecutionCompileTest\analysefilessecond.i,C:\Users\Julien\analysefiles.r")]
        public void GetReferencedFilesFromFileIdLog_Test(string fileContent, string expected) {
            File.WriteAllText(Path.Combine(TestFolder, "test.fileidlog"), fileContent, Encoding.Default);
            var l = UoeUtilities.GetReferencedFilesFromFileIdLog(Path.Combine(TestFolder, "test.fileidlog"), Encoding.Default);
            Assert.AreEqual(expected, string.Join(",", l));
        }

    }
}