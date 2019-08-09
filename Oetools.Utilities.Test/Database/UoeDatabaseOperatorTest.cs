#region header
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
using Oetools.Utilities.Openedge.Database.Exceptions;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Database {

    [TestClass]
    public class UoeDatabaseOperatorTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeDatabaseOperatorTest)));

        [ClassInitialize]
        public static void Init(TestContext context) {
            Cleanup();
            Directory.CreateDirectory(TestFolder);
        }

        [ClassCleanup]
        public static void Cleanup() {
            UoeDatabaseOperator.KillAllMproSrv();

            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }

        private UoeDatabaseLocation GetDb(string name) {
            return new UoeDatabaseLocation(Path.Combine(TestFolder, name));
        }

        [TestMethod]
        public void GetNextAvailablePort() {
            Assert.IsTrue(UoeDatabaseOperator.GetNextAvailablePort(0) > 0);
            Assert.IsTrue(UoeDatabaseOperator.GetNextAvailablePort(1025) >= 1025);
        }

        [TestMethod]
        public void ReadLogFileTest() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            var ope = new UoeDatabaseOperator(dlcPath);

            var lgPAth = Path.Combine(TestFolder, "ReadLogFileTest.lg");
            File.WriteAllText(lgPAth, @"
                Tue Jan  1 15:28:10 2019
[2019/01/01@14:46:51.345+0100] P-10860      T-17952 I BROKER  0: (333)   Multi-user session begin.
[2019/01/01@14:46:51.345+0100] P-10860      T-15580 I BROKER  0: (4393)  This server is licenced for local logins only.
[2019/01/01@14:46:51.345+0100] P-10860      T-7584  I BROKER  0: (4261)  Host Name (-H): hostname
[2019/01/01@14:46:51.349+0100] P-10860      T-7584  I BROKER  0: (4262)  Service Name (-S): 1
");
            ope.ReadStartingParametersFromLogFile(lgPAth, out string hostName, out string serviceName);

            Assert.IsNotNull(hostName);
            Assert.IsNotNull(serviceName);

            Assert.AreEqual(@"localhost", hostName);
            Assert.AreEqual(@"1", serviceName);

            File.WriteAllText(lgPAth, @"
                Tue Jan  1 15:28:10 2019
[2019/01/01@14:46:51.345+0100] P-10860      T-17952 I BROKER  0: (333)   Multi-user session begin.
[2019/01/01@14:46:51.345+0100] P-10860      T-7584  I BROKER  0: (4261)  Host Name (-H): hostname
[2019/01/01@14:46:51.349+0100] P-10860      T-7584  I BROKER  0: (4262)  Service Name (-S): 999
");
            ope.ReadStartingParametersFromLogFile(lgPAth, out hostName, out serviceName);

            Assert.IsNotNull(hostName);
            Assert.IsNotNull(serviceName);

            Assert.AreEqual(@"hostname", hostName);
            Assert.AreEqual(@"999", serviceName);

            File.WriteAllText(lgPAth, @"
                Tue Jan  1 15:28:10 2019
[2019/01/01@14:46:51.345+0100] P-10860      T-17952 I BROKER  0: (333)   Multi-user session begin.
[2019/01/01@14:46:51.345+0100] P-10860      T-7584  I BROKER  0: (4261)  Host Name (-H): hostname
[2019/01/01@14:46:51.349+0100] P-10860      T-7584  I BROKER  0: (4262)  Service Name (-S): 0
");
            ope.ReadStartingParametersFromLogFile(lgPAth, out hostName, out serviceName);

            Assert.AreEqual(null, hostName);
            Assert.AreEqual(null, serviceName);

            File.WriteAllText(lgPAth, @"
                Tue Jan  1 15:28:10 2019
[2019/01/01@14:46:51.345+0100] P-10860      T-17952 I BROKER  0: (333)   Multi-user session begin.
[2019/01/01@14:46:51.345+0100] P-10860      T-7584  I BROKER  0: (4261)  Host Name (-H): hostname
[2019/01/01@14:46:51.349+0100] P-10860      T-7584  I BROKER  0: (4262)  Service Name (-S): 0
[2019/01/01@14:46:51.349+0100] P-10999      T-7584  I SRV     1: (5646)  Started on port 3000 using TCP IPV4 address 127.0.0.1, pid 17372.
[2019/01/01@14:46:51.349+0100] P-11111      T-6060  I SQLSRV2 1: (-----) SQL Server 11.7.04 started, configuration: ""db.virtualconfig""
");
            var pids = ope.GetPidsFromLogFile(lgPAth).ToList();

            Assert.AreEqual(11111, pids[0]);
            Assert.AreEqual(10999, pids[1]);
            Assert.AreEqual(10860, pids[2]);
        }



        [TestMethod]
        public void ValidateStructureFile() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            var pathSt = Path.Combine(TestFolder, "validate.st");
            File.WriteAllText(pathSt, "b .\nd \"Schema Area\":6,32;1 .\nd \"Data\":11,32;1 .");

            var ope = new UoeDatabaseOperator(dlcPath);

            ope.ValidateStructureFile(GetDb("data"), pathSt);

            File.WriteAllText(pathSt, "z \"Schema Area\":6,32;1 .\nd \"Order\":11,32;1 . f 1280 \nd \"Order\":11,32;1 .");

            Assert.ThrowsException<UoeDatabaseException>(() => ope.ValidateStructureFile(GetDb("data"), pathSt));
        }

        [TestMethod]
        public void Create() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("test1.db");

            ope.Create(db);

            Assert.IsTrue(db.Exists());

            ope.Delete(db);

            Assert.IsFalse(db.Exists());

            var stPath = Path.Combine(TestFolder, "test1.st");
            File.WriteAllText(stPath, "b .\nd \"Schema Area\" .\nd \"data\" .");

            ope.Create(db, stPath);

            Assert.IsTrue(db.Exists());
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "test1_7.d1")));

            ope.Delete(db);

            Assert.IsFalse(db.Exists());

            File.WriteAllText(stPath, "b .\nd \"Schema Area\" ./sub\nd \"data\" ./data");

            ope.Create(db, stPath, DatabaseBlockSize.S4096, "utf", false, false);

            Assert.IsTrue(db.Exists());

            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "sub", "test1.d1")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "data", "test1_7.d1")));

            ope.Delete(db);

            Assert.IsFalse(File.Exists(Path.Combine(TestFolder, "sub", "test1.d1")));
            Assert.IsFalse(File.Exists(Path.Combine(TestFolder, "data", "test1_7.d1")));
        }

        [TestMethod]
        public void Copy() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);

            var stPath = Path.Combine(TestFolder, "copysource.st");
            File.WriteAllText(stPath, $"b .\nd \"Schema Area\" \"{TestFolder}\"");

            var srcDb = GetDb("copysource");
            var tgtDb = GetDb("copytarget");

            ope.Create(srcDb, stPath);
            Assert.IsTrue(srcDb.Exists());

            ope.Copy(tgtDb, srcDb, false, false);
            Assert.IsTrue(tgtDb.Exists());

            ope.Delete(tgtDb);
            Assert.IsFalse(tgtDb.Exists());

            ope.Copy(tgtDb, srcDb);
            Assert.IsTrue(tgtDb.Exists());
        }

        [TestMethod]
        public void UpdateStructureFile() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);

            var tgtDb = GetDb("updatest");

            ope.Create(tgtDb);
            Assert.IsTrue(tgtDb.Exists());

            File.Delete(tgtDb.StructureFileFullPath);

            ope.UpdateStructureFile(tgtDb, false, false);

            Assert.IsTrue(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(Path.Combine(tgtDb.DirectoryPath, "updatest.b1")));

            File.Delete(tgtDb.StructureFileFullPath);

            ope.UpdateStructureFile(tgtDb, false, true);

            Assert.IsFalse(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(Path.Combine(tgtDb.DirectoryPath, "updatest.b1")));
            Assert.IsTrue(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(tgtDb.DirectoryPath));

            File.Delete(tgtDb.StructureFileFullPath);

            ope.UpdateStructureFile(tgtDb, true, false);

            Assert.IsFalse(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(Path.Combine(tgtDb.DirectoryPath, "updatest.b1")));
            Assert.IsFalse(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(tgtDb.DirectoryPath));
            Assert.IsTrue(File.ReadAllText(tgtDb.StructureFileFullPath).Contains("updatest.b1"));

            File.Delete(tgtDb.StructureFileFullPath);

            ope.UpdateStructureFile(tgtDb, true, true);

            Assert.IsFalse(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(Path.Combine(tgtDb.DirectoryPath, "updatest.b1")));
            Assert.IsFalse(File.ReadAllText(tgtDb.StructureFileFullPath).Contains(tgtDb.DirectoryPath));
            Assert.IsFalse(File.ReadAllText(tgtDb.StructureFileFullPath).Contains("updatest.b1"));
            Assert.IsTrue(File.ReadAllText(tgtDb.StructureFileFullPath).Contains("."));
        }

        [TestMethod]
        public void RepairDatabaseControlInfo() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);

            var tgtDb = GetDb("repair");

            ope.Create(tgtDb);
            Assert.IsTrue(tgtDb.Exists());

            var stPath = Path.Combine(TestFolder, "repair.st");
            File.WriteAllText(stPath, $"b .\nd \"Schema Area\" \"{TestFolder}\"");

            ope.RepairDatabaseControlInfo(tgtDb);

            File.WriteAllText(stPath, "b .\nd \"Schema Area\" .");

            File.Delete(tgtDb.FullPath);
            ope.RepairDatabaseControlInfo(tgtDb);

            tgtDb.ThrowIfNotExist();
        }

        [TestMethod]
        public void AddAndRemoveExtents() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);

            var tgtDb = GetDb("extents");

            ope.Create(tgtDb);
            Assert.IsTrue(tgtDb.Exists());

            var stPath = Path.Combine(TestFolder, "extents_add.st");
            File.WriteAllText(stPath, $"d \"Data Area\" \"{TestFolder}\"");

            ope.AddStructureDefinition(tgtDb, stPath);

            try {
                ope.Start(tgtDb);
                File.WriteAllText(stPath, $"d \"New Area\" \"{TestFolder}\"");
                ope.AddStructureDefinition(tgtDb, stPath);
            } finally {
                ope.Stop(tgtDb);
            }

            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "extents_7.d1")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "extents_8.d1")));

            ope.RemoveStructureDefinition(tgtDb, "d", "Data Area");

            Assert.IsFalse(File.Exists(Path.Combine(TestFolder, "extents_7.d1")));
        }

        [TestMethod]
        public void Start() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);

            var tgtDb = GetDb("start");

            ope.Create(tgtDb);
            Assert.IsTrue(tgtDb.Exists());

            Assert.AreEqual(DatabaseBusyMode.NotBusy, ope.GetBusyMode(tgtDb));

            var nextPort = UoeDatabaseOperator.GetNextAvailablePort();

            try {
                ope.Start(tgtDb, "localhost", nextPort.ToString(), new UoeProcessArgs().Append("-minport", "50000", "-maxport", "50100", "-L", "20000") as UoeProcessArgs);

                Assert.AreEqual(DatabaseBusyMode.MultiUser, ope.GetBusyMode(tgtDb));

            } finally {
                ope.Kill(tgtDb);
            }

            Assert.AreEqual(DatabaseBusyMode.NotBusy, ope.GetBusyMode(tgtDb));
        }

        [TestMethod]
        public void Delete() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);

            var deleteDir = Path.Combine(TestFolder, "delete");
            Directory.CreateDirectory(deleteDir);

            var stPath = Path.Combine(deleteDir, "test1.st");
            File.WriteAllText(stPath, @"
b .
d ""Schema Area"":6,32;1 .
d ""Data Area"":7,32;1 .
d ""Index Area"":8,32;8 .
d ""Data Area2"":9,32;8 .
d ""Data Area3"":12,32;8 .
");
            var loc = new UoeDatabaseLocation(Path.Combine(deleteDir, "test1.db"));
            db.Create(loc, stPath);

            Assert.AreEqual(9, Directory.EnumerateFiles(deleteDir, "*", SearchOption.TopDirectoryOnly).Count());

            File.Delete(stPath);

            db.Delete(loc);

            Assert.AreEqual(1, Directory.EnumerateFiles(deleteDir, "*", SearchOption.TopDirectoryOnly).Count(), "only the .st should be left (it has been recreated to list files to delete).");

            File.WriteAllText(stPath, @"
b ./2
a ./3 f 1024
a ./3 f 1024
a !""./3"" f 1024
t . f 4096
d ""Employee"",32 ./1/emp f 1024
d ""Employee"",32 ./1/emp
d ""Inventory"",32 ./1/inv f 1024
d ""Inventory"",32 ./1/inv
d ""Cust_Data"",32;64 ./1/cust f 1024
d ""Cust_Data"",32;64 ./1/cust
d ""Cust_Index"",32;8 ./1/cust
d ""Order"",32;64 ./1/ord f 1024
d ""Order"",32;64 ./1/ord
d ""Misc"",32 !""./1/misc data"" f 1024
d ""Misc"",32 !""./1/misc data""
d ""schema Area"" .
");

            db.CreateVoidDatabase(loc, stPath);

            Assert.AreEqual(20, Directory.EnumerateFiles(deleteDir, "*", SearchOption.AllDirectories).Count());

            db.Delete(loc);

            Assert.AreEqual(1, Directory.EnumerateFiles(deleteDir, "*", SearchOption.AllDirectories).Count());
        }

        [TestMethod]
        public void GenerateStructureFileFromDf() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("generatedf");

            var pathDf = Path.Combine(TestFolder, "generatedf.df");

            File.WriteAllText(pathDf, "ADD TABLE \"Benefits\"\n  AREA \"Employee\"\n  DESCRIPTION \"The benefits table contains employee benefits.\"\n  DUMP-NAME \"benefits\"\n\nADD TABLE \"BillTo\"\n  AREA \"Order\"\n  DESCRIPTION \"The billto table contains bill to address information for an order. \"\n  DUMP-NAME \"billto\"\n");

            var generatedSt = ope.GenerateStructureFileFromDf(db, pathDf);

            Assert.AreEqual(db.StructureFileFullPath, generatedSt);
            Assert.IsTrue(File.Exists(generatedSt));

            Assert.IsTrue(File.ReadAllText(generatedSt).Contains("Employee"));
            Assert.IsTrue(File.ReadAllText(generatedSt).Contains("Order"));

            File.WriteAllText(pathDf, "");

            ope.GenerateStructureFileFromDf(db, pathDf);
            Assert.IsTrue(File.ReadAllText(generatedSt).Contains("Schema Area"));
        }

        [TestMethod]
        public void GetConnectionString() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("connec");

            ope.Create(db);

            Assert.IsTrue(ope.GetDatabaseConnection(db).SingleUser);
            Assert.IsTrue(string.IsNullOrEmpty(ope.GetDatabaseConnection(db).Service));
            Assert.AreEqual(null, ope.GetDatabaseConnection(db).LogicalName);
            Assert.AreEqual("test", ope.GetDatabaseConnection(db, "test").LogicalName);

            try {
                ope.Start(db);

                Assert.IsFalse(ope.GetDatabaseConnection(db).SingleUser);
                Assert.IsTrue(string.IsNullOrEmpty(ope.GetDatabaseConnection(db).Service));
            } finally {
                ope.Kill(db);
            }

            try {
                ope.Start(db, null, UoeDatabaseOperator.GetNextAvailablePort().ToString());

                Assert.IsFalse(ope.GetDatabaseConnection(db).SingleUser);
                Assert.IsFalse(string.IsNullOrEmpty(ope.GetDatabaseConnection(db).Service));
                Assert.IsFalse(string.IsNullOrEmpty(ope.GetDatabaseConnection(db).HostName));
            } finally {
                ope.Kill(db);
            }
        }

        [TestMethod]
        public void TruncateLog() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("trunclog");

            ope.Create(db);
            Assert.IsTrue(db.Exists());

            ope.TruncateLog(db);

            try {
                ope.Start(db);

                var oldSize = new FileInfo(Path.Combine(TestFolder, "trunclog.lg")).Length;

                ope.TruncateLog(db);

                var newSize = new FileInfo(Path.Combine(TestFolder, "trunclog.lg")).Length;

                Assert.AreNotEqual(oldSize, newSize);

                var cs = ope.GetDatabaseConnection(db);
                Assert.IsFalse(cs.SingleUser);
                Assert.IsTrue(string.IsNullOrEmpty(cs.Service));
            } finally {
                ope.Kill(db);
            }
        }

        [TestMethod]
        public void Analysis() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("analys");

            ope.Create(db);
            Assert.IsTrue(db.Exists());

            Assert.IsTrue(ope.GenerateAnalysisReport(db).Length > 10);
        }

        [TestMethod]
        public void BinaryDumpLoadIdxRebuild() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            // create .df
            var dfPath = Path.Combine(TestFolder, "bindb.df");
            File.WriteAllText(dfPath, "ADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD FIELD \"field2\" OF \"table1\" AS integer \n  DESCRIPTION \"field two\"\n  FORMAT \"9\"\n  INITIAL 0\n  POSITION 3\n  ORDER 20\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                var db = GetDb("dumploadbin");
                dataAdmin.CreateWithDf(db, dfPath);

                var dataDirectory = Path.Combine(TestFolder, "bindb_data");
                Directory.CreateDirectory(dataDirectory);
                try {
                    var table1Path = Path.Combine(dataDirectory, "table1.d");
                    // load data from .d
                    File.WriteAllText(table1Path, "\"value1\" 1\n\"value2\" 2\n");
                    dataAdmin.LoadData(dataAdmin.GetDatabaseConnection(db), dataDirectory);
                    File.Delete(table1Path);

                    // dump binary
                    dataAdmin.DumpBinaryData(db, "table1", dataDirectory);
                    var binDataFilePath = Path.Combine(dataDirectory, "table1.bd");
                    Assert.IsTrue(File.Exists(binDataFilePath));

                    // recreate db
                    dataAdmin.Delete(db);
                    dataAdmin.CreateWithDf(db, dfPath);

                    // load binary
                    dataAdmin.LoadBinaryData(db, binDataFilePath);

                    // re-rebuild index
                    dataAdmin.RebuildIndexes(db, null, new UoeProcessArgs().Append("table", "table1") as UoeProcessArgs);

                    // dump data .d
                    dataAdmin.DumpData(dataAdmin.GetDatabaseConnection(db), dataDirectory, "table1");
                    Assert.IsTrue(File.Exists(table1Path));
                    var dataContent = File.ReadAllText(table1Path);
                    Assert.IsTrue(dataContent.Contains("\"value1\" 1"));

                    // truncate table
                    dataAdmin.TruncateTableData(dataAdmin.GetDatabaseConnection(db), "table1");

                    // dump empty data .d
                    dataAdmin.DumpData(dataAdmin.GetDatabaseConnection(db), dataDirectory, "table1");
                    Assert.IsFalse(File.ReadAllText(table1Path).Contains("\"value1\" 1"));

                } finally {
                    Directory.Delete(dataDirectory, true);
                }
            }

        }

        [TestMethod]
        public void Backup_Restore() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("backuprest");

            ope.Copy(db, new UoeDatabaseLocation(Path.Combine(dlcPath, "sports2000")));
            Assert.IsTrue(db.Exists());

            var backupPath = Path.Combine(TestFolder, "backup.bkp");
            ope.Backup(db, backupPath);

            ope.Delete(db);
            Assert.IsFalse(db.Exists());

            ope.Restore(db, backupPath);
            Assert.IsTrue(db.Exists());
        }

        [TestMethod]
        public void BulkLoad() {
            // TODO: test BulkLoad.
        }
    }

}
