#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseAdministratorTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Database.Exceptions;

namespace Oetools.Utilities.Test.Openedge.Database {

    [TestClass]
    public class UoeDatabaseAdministratorTest {

        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeDatabaseAdministratorTest)));

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

        private UoeDatabaseLocation GetDb(string name) {
            return new UoeDatabaseLocation(Path.Combine(TestFolder, name));
        }

        [TestMethod]
        public void LoadSchemaDefinition() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var ope = new UoeDatabaseOperator(dlcPath);
            var db = GetDb("loaddf");
            var dfPath = Path.Combine(TestFolder, $"{db.PhysicalName}.df");

            ope.Create(db);
            Assert.IsTrue(db.Exists());

            // create .df
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.LoadSchemaDefinition(UoeDatabaseConnection.NewSingleUserConnection(db), dfPath);
            }

            ope.Delete(db);
            ope.Create(db);

            // create .df
            File.WriteAllText(dfPath, "ADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                Assert.ThrowsException<UoeDatabaseException>(() => dataAdmin.LoadSchemaDefinition(UoeDatabaseConnection.NewSingleUserConnection(db), dfPath));
            }
        }

        [TestMethod]
        public void CreateDatabase() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = GetDb("createdf");
            var dfPath = Path.Combine(TestFolder, $"{db.PhysicalName}.df");

            // create .df
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateWithDf(db, dfPath);
                Assert.IsTrue(db.Exists());
            }
        }

        [TestMethod]
        public void DumpDf() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = GetDb("dumpdf");
            var dfPath = Path.Combine(TestFolder, $"{db.PhysicalName}.df");

            // create .df
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateWithDf(db, dfPath);

                var dfPathOut = Path.Combine(TestFolder, "dumpdf_out.df");
                dataAdmin.DumpSchemaDefinition(UoeDatabaseConnection.NewSingleUserConnection(db), dfPathOut);

                Assert.IsTrue(File.Exists(dfPathOut));
                Assert.IsTrue(File.ReadAllText(dfPathOut).Contains("field1"));
            }
        }

        [TestMethod]
        public void DumpIncrementalSchemaDefinition() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            // create 2 .df
            var prevDfPath = Path.Combine(TestFolder, "df_previous.df");
            File.WriteAllText(prevDfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD FIELD \"field2\" OF \"table1\" AS character \n  DESCRIPTION \"field two\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 3\n  MAX-WIDTH 16\n  ORDER 20\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            var newDfPath = Path.Combine(TestFolder, "df_new.df");
            File.WriteAllText(newDfPath, "ADD SEQUENCE \"sequence2\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD FIELD \"field3\" OF \"table1\" AS character \n  DESCRIPTION \"field three\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 3\n  MAX-WIDTH 16\n  ORDER 20\n\nADD TABLE \"table2\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table two\"\n  DUMP-NAME \"table2\"\n\nADD FIELD \"field1\" OF \"table2\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                var dfPathOut = Path.Combine(TestFolder, "inc_out.df");
                dataAdmin.DumpIncrementalSchemaDefinition(prevDfPath, newDfPath, dfPathOut);
                Assert.IsTrue(File.Exists(dfPathOut));
                var incrementalContent = File.ReadAllText(dfPathOut);
                Assert.IsTrue(incrementalContent.Contains("ADD TABLE \"table2\""));
                Assert.IsTrue(incrementalContent.Contains("ADD FIELD \"field1\" OF \"table2\""));
                Assert.IsTrue(incrementalContent.Contains("ADD FIELD \"field3\" OF \"table1\""));
                Assert.IsTrue(incrementalContent.Contains("DROP FIELD \"field2\" OF \"table1\""));
                Assert.IsTrue(incrementalContent.Contains("DROP SEQUENCE \"sequence1\""));
                Assert.IsTrue(incrementalContent.Contains("ADD SEQUENCE \"sequence2\""));

                var renameFilePath = Path.Combine(TestFolder, "rename.d");
                File.WriteAllText(renameFilePath, "F,table1,field2,field3\nS,sequence1,sequence2");
                dataAdmin.DumpIncrementalSchemaDefinition(prevDfPath, newDfPath, dfPathOut, renameFilePath);
                Assert.IsTrue(File.Exists(dfPathOut));
                incrementalContent = File.ReadAllText(dfPathOut);
                Assert.IsTrue(incrementalContent.Contains("RENAME FIELD \"field2\" OF \"table1\" TO \"field3\""));
                Assert.IsTrue(incrementalContent.Contains("UPDATE FIELD \"field3\" OF \"table1\""));
                // for sequences, they are still dropped/added
                Assert.IsTrue(incrementalContent.Contains("DROP SEQUENCE \"sequence1\""));
                Assert.IsTrue(incrementalContent.Contains("ADD SEQUENCE \"sequence2\""));
            }

        }


        [TestMethod]
        public void SequenceData() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = GetDb("seqdata");

            // create .df
            var dfPath = Path.Combine(TestFolder, "seqdata.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD SEQUENCE \"sequence2\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateWithDf(db, dfPath);

                var sequenceDataFilePath = Path.Combine(TestFolder, $"{db.PhysicalName}.d");

                // load seq
                File.WriteAllText(sequenceDataFilePath, "0 \"sequence1\" 20\n");
                dataAdmin.LoadSequenceData(dataAdmin.GetDatabaseConnection(db), sequenceDataFilePath);

                // dump seq
                File.Delete(sequenceDataFilePath);
                dataAdmin.DumpSequenceData(dataAdmin.GetDatabaseConnection(db), sequenceDataFilePath);

                Assert.IsTrue(File.Exists(sequenceDataFilePath));
                var dataContent = File.ReadAllText(sequenceDataFilePath);
                Assert.IsTrue(dataContent.Contains("\"sequence1\" 20"));
                Assert.IsTrue(dataContent.Contains("\"sequence2\" 0"));
            }

        }

        [TestMethod]
        public void Data() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = GetDb("datad");

            // create .df
            var dfPath = Path.Combine(TestFolder, "data.df");
            File.WriteAllText(dfPath, "ADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD FIELD \"field2\" OF \"table1\" AS integer \n  DESCRIPTION \"field two\"\n  FORMAT \"9\"\n  INITIAL 0\n  POSITION 3\n  ORDER 20\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateWithDf(db, dfPath);

                var dataDirectory = Path.Combine(TestFolder, "data");
                Directory.CreateDirectory(dataDirectory);
                try {
                    var table1Path = Path.Combine(dataDirectory, "table1.d");
                    // load data
                    File.WriteAllText(table1Path, "\"value1\" 1\n\"value2\" 2\n");
                    dataAdmin.LoadData(dataAdmin.GetDatabaseConnection(db), dataDirectory);

                    // dump seq
                    File.Delete(table1Path);
                    dataAdmin.DumpData(dataAdmin.GetDatabaseConnection(db), dataDirectory);

                    Assert.IsTrue(File.Exists(table1Path));
                    var dataContent = File.ReadAllText(table1Path);
                    Assert.IsTrue(dataContent.Contains("\"value1\" 1"));
                    Assert.IsTrue(dataContent.Contains("\"value2\" 2"));

                } finally {
                    Directory.Delete(dataDirectory, true);
                }
            }

        }

        [TestMethod]
        public void Sql() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            // create .df
            var dfPath = Path.Combine(TestFolder, "sql.df");
            File.WriteAllText(dfPath, "ADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD FIELD \"field2\" OF \"table1\" AS integer \n  DESCRIPTION \"field two\"\n  FORMAT \"9\"\n  INITIAL 0\n  POSITION 3\n  ORDER 20\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                var db = GetDb("sql");
                dataAdmin.CreateWithDf(db, dfPath);

                var dbConnect = dataAdmin.GetDatabaseConnection(db);

                // load data from .d
                var table1Path = Path.Combine(TestFolder, "table1.d");
                File.WriteAllText(table1Path, "\"value1\" 1\n\"value2\" 2\n");
                dataAdmin.LoadData(dbConnect, TestFolder);
                File.Delete(table1Path);

                // dump sql schema
                var dump = Path.Combine(TestFolder, $"out{UoeDatabaseLocation.SqlSchemaExtension}");
                dataAdmin.DumpSqlSchema(dbConnect, dump);
                Assert.IsTrue(File.Exists(dump));

            }

        }
    }
}
