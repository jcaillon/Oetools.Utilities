﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseOperatorTest.cs) is part of Oetools.Utilities.Test.
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Database;

namespace Oetools.Utilities.Test.Openedge.Database {
    
    [TestClass]
    public class UoeDatabaseOperatorTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeDatabaseOperatorTest)));

        [ClassInitialize]
        public static void Init(TestContext context) {           
            Cleanup();
            Directory.CreateDirectory(TestFolder);
            
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            db.Procopy(Path.Combine(TestFolder, "ref.db"), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "ref.db")));
        }
        
        [ClassCleanup]
        public static void Cleanup() {
            UoeDatabaseOperator.KillAllMproSrv();
            
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }

        [DataRow(@"", true)]
        [DataRow(null, true)]
        [DataRow("123456789012345678901234567890123", true, DisplayName = "33 chars is too much")]
        [DataRow(@"azée", true, DisplayName = "contains invalid accent char")]
        [DataRow(@"zeffez zezffe", true, DisplayName = "spaces")]
        [DataRow(@"0ezzef", true, DisplayName = "first should be a letter")]
        [DataRow(@"az_-zefze", false, DisplayName = "ok")]
        [DataTestMethod]
        public void ValidateLogicalName_Test(string input, bool exception) {
            if (exception) {
                Assert.ThrowsException<UoeDatabaseOperationException>(() => UoeDatabaseOperator.ValidateLogicalName(input));
            } else {
                UoeDatabaseOperator.ValidateLogicalName(input);
            }
        }

        [DataRow(@"", "unnamed")]
        [DataRow(null, "unnamed")]
        [DataRow("bouhé!", "bouh")]
        [DataRow("truc db", "trucdb")]
        [DataRow("éééééé@", "unnamed")]
        [DataRow("123456789012345678901234567890123456789", "12345678901234567890123456789012")]
        [DataTestMethod]
        public void GetValidLogicalName_Test(string input, string expect) {
            Assert.AreEqual(expect, UoeDatabaseOperator.GetValidLogicalName(input));
        }

        [DataRow(@"", "unnamed")]
        [DataRow(null, "unnamed")]
        [DataRow("bouhé!", "bouh")]
        [DataRow("truc db", "trucdb")]
        [DataRow("éééééé@", "unnamed")]
        [DataRow("123456789012345678901234567890123456789", "12345678901")]
        [DataTestMethod]
        public void GetValidPhysicalName_Test(string input, string expect) {
            Assert.AreEqual(expect, UoeDatabaseOperator.GetValidPhysicalName(input));
        }
        
        [TestMethod]
        public void GetNextAvailablePort_Ok() {
            Assert.IsTrue(UoeDatabaseOperator.GetNextAvailablePort(0) > 0);
            Assert.IsTrue(UoeDatabaseOperator.GetNextAvailablePort(1025) >= 1025);
        }
        
        [TestMethod]
        public void ProstrctCreate_Ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
            var stPath = db.CreateStandardStructureFile(Path.Combine(TestFolder, "test1.db"));
            
            db.ProstrctCreate(Path.Combine(TestFolder, "test1.db"), stPath, DatabaseBlockSize.S1024);
            
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test1.db")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test1.d1")));

        }
        
        [TestMethod]
        public void Procopy_empty_no_options_after_create_Ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);

            var stPath = db.CreateStandardStructureFile(Path.Combine(TestFolder, "test2.db"));
            
            db.ProstrctCreate(Path.Combine(TestFolder, "test2.db"), stPath, DatabaseBlockSize.S1024);
            
            db.Procopy(Path.Combine(TestFolder, "test2.db"), DatabaseBlockSize.S1024);
            
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test2.db")));

        }
        
        [TestMethod]
        public void Procopy_empty_no_options_Ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
            db.Procopy(Path.Combine(TestFolder, "test3.db"), DatabaseBlockSize.S1024);
            
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test3.db")));

        }
        
        [TestMethod]
        public void Procopy_empty_with_options_then_delete_Ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
            db.Procopy(Path.Combine(TestFolder, "test4.db"), DatabaseBlockSize.S8192, "utf", false, false);
            
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test4.db")));
            
            db.Delete(Path.Combine(TestFolder, "test4.db"));
            
            Assert.IsFalse(File.Exists(Path.Combine(TestFolder, "test4.db")));
        }
        
        [TestMethod]
        public void GenerateStructureFile() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var db = new UoeDatabaseOperator(dlcPath);

            var pathDf = Path.Combine(TestFolder, "file.df");

            File.WriteAllText(pathDf, "ADD TABLE \"Benefits\"\n  AREA \"Employee\"\n  DESCRIPTION \"The benefits table contains employee benefits.\"\n  DUMP-NAME \"benefits\"\n\nADD TABLE \"BillTo\"\n  AREA \"Order\"\n  DESCRIPTION \"The billto table contains bill to address information for an order. \"\n  DUMP-NAME \"billto\"\n");

            var generatedSt = db.GenerateStructureFileFromDf(Path.Combine(TestFolder, "file.db"), pathDf);
            
            Assert.AreEqual(Path.Combine(TestFolder, "file.st"), generatedSt);
            
            Assert.IsTrue(File.Exists(generatedSt));
            
            Assert.IsTrue(File.ReadAllText(Path.Combine(TestFolder, "file.st")).Contains("Employee"));
            Assert.IsTrue(File.ReadAllText(Path.Combine(TestFolder, "file.st")).Contains("Order"));

        }
        
        [TestMethod]
        public void CopyStructureFile() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var db = new UoeDatabaseOperator(dlcPath);

            var pathSt = Path.Combine(TestFolder, "source.st");

            File.WriteAllText(pathSt, "d \"Schema Area\":6,32;1 .\nd \"Order\":11,32;1 D:\\DATABASES\\cp_sport f 1280 \nd \"Order\":11,32;1 \"D:\\DATABASES\\cp_sport\"");

            var generatedSt = db.CopyStructureFile(Path.Combine(TestFolder, "target.db"), pathSt);
            
            Assert.AreEqual(Path.Combine(TestFolder, "target.st"), generatedSt);
            Assert.IsTrue(File.Exists(generatedSt));
            
            Assert.IsTrue(File.ReadAllText(Path.Combine(TestFolder, "target.st")).Count(c => c.Equals('.')).Equals(3));

        }
        
        [TestMethod]
        public void GenerateStructureFile_empty_df() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            var db = new UoeDatabaseOperator(dlcPath);

            var pathDf = Path.Combine(TestFolder, "file.df");
            
            File.WriteAllText(pathDf, "");
            
            db.GenerateStructureFileFromDf(Path.Combine(TestFolder, "file.db"), pathDf);
            
            Assert.IsTrue(File.ReadAllText(Path.Combine(TestFolder, "file.st")).Contains("Schema Area"));
        }
        
        
        [TestMethod]
        public void Tests_on_base_ref() {
            Procopy_existing_db();
            ProstrctRepair_ok();
            GetBusyMode_isnone_ok();
            Proserve_simple();
            ProShut_normal_ok();
            Proserve_with_options();
            ProShut_hard_ok();
        }

        private void Procopy_existing_db() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
            db.Procopy(Path.Combine(TestFolder, "test5.db"), Path.Combine(TestFolder, "ref.db"), false, false);
            
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test5.db")));

        }
        
        private void ProstrctRepair_ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            db.ProstrctRepair(Path.Combine(TestFolder, "ref.db"));
        }
        
        private void GetBusyMode_isnone_ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            Assert.AreEqual(DatabaseBusyMode.NotBusy, db.GetBusyMode(Path.Combine(TestFolder, "ref.db")));
        }

        private void ProShut_hard_ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
            UoeDatabaseOperator.KillAllMproSrv();

            Assert.AreEqual(DatabaseBusyMode.NotBusy, db.GetBusyMode(Path.Combine(TestFolder, "ref.db")));
        }

        private void ProShut_normal_ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
            db.Proshut(Path.Combine(TestFolder, "ref.db"));
            
            Assert.AreEqual(DatabaseBusyMode.NotBusy, db.GetBusyMode(Path.Combine(TestFolder, "ref.db")));
        }

        private void Proserve_with_options() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            var nextPort = UoeDatabaseOperator.GetNextAvailablePort(0);
            
            Assert.IsTrue(nextPort > 0);
                
            db.ProServe(Path.Combine(TestFolder, "ref.db"), nextPort, 20, "-minport 50000 -maxport 50100 -L 20000");
            // https://community.progress.com/community_groups/openedge_rdbms/f/18/t/9300
            
            Assert.AreEqual(DatabaseBusyMode.MultiUser, db.GetBusyMode(Path.Combine(TestFolder, "ref.db")));
        }

        private void Proserve_simple() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            
                
            db.ProServe(Path.Combine(TestFolder, "ref.db"));
            // https://community.progress.com/community_groups/openedge_rdbms/f/18/t/9300
            
            Assert.AreEqual(DatabaseBusyMode.MultiUser, db.GetBusyMode(Path.Combine(TestFolder, "ref.db")));
        }


        
    }
}