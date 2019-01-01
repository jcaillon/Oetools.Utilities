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
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Database;

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

        [TestMethod]
        public void Load_df_Ok() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            db.Procopy(Path.Combine(TestFolder, "ref.db"), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "ref.db")));

            // create .df
            var dfPath = Path.Combine(TestFolder, "ref.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.LoadSchemaDefinition(Path.Combine(TestFolder, "ref.db"), dfPath);
            }
        }

        [TestMethod]
        public void Load_df_Should_Fail() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var db = new UoeDatabaseOperator(dlcPath);
            db.Procopy(Path.Combine(TestFolder, "ref2.db"), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "ref2.db")));

            // create .df
            var dfPath = Path.Combine(TestFolder, "ref2.df");
            File.WriteAllText(dfPath, "ADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n");

            Exception ex = null;
            try {
                using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                    dataAdmin.LoadSchemaDefinition(Path.Combine(TestFolder, "ref2.db"), dfPath);
                }
            } catch (Exception e) {
                ex = e;
            }
            Assert.IsNotNull(ex);
        }

        [TestMethod]
        public void CreateDatabase() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateDatabase(Path.Combine(TestFolder, "created1.db"));
                Assert.IsTrue(dataAdmin.GetBusyMode(Path.Combine(TestFolder, "created1.db")).Equals(DatabaseBusyMode.NotBusy));
            }

            // create .df
            var dfPath = Path.Combine(TestFolder, "ref.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateDatabase(Path.Combine(TestFolder, "created2.db"), dataAdmin.CreateStandardStructureFile(Path.Combine(TestFolder, "created2.db")), DatabaseBlockSize.S2048, null, true, true, dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(Path.Combine(TestFolder, "created2.db")).Equals(DatabaseBusyMode.NotBusy));
            }

        }

        [TestMethod]
        public void CreateCompilationDatabase() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            // create .df
            var dfPath = Path.Combine(TestFolder, "compil.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                dataAdmin.CreateCompilationDatabaseFromDf(Path.Combine(TestFolder, "compil.db"), dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(Path.Combine(TestFolder, "compil.db")).Equals(DatabaseBusyMode.NotBusy));
            }

        }

        [TestMethod]
        public void DumpDf() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            // create .df
            var dfPath = Path.Combine(TestFolder, "dumpdf_in.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD INDEX \"idx_1\" ON \"table1\" \n  AREA \"Schema Area\"\n  PRIMARY\n  INDEX-FIELD \"field1\" ASCENDING\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                var dbPath = Path.Combine(TestFolder, "dumpdf_in.db");
                dataAdmin.CreateCompilationDatabaseFromDf(dbPath, dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(dbPath).Equals(DatabaseBusyMode.NotBusy));

                var dfPathOut = Path.Combine(TestFolder, "dumpdf_out.df");
                dataAdmin.DumpSchemaDefinition(dbPath, dfPathOut);

                Assert.IsTrue(File.Exists(dfPathOut));
                Assert.IsTrue(File.ReadAllText(dfPathOut).Contains("field1"));
            }

        }

        [TestMethod]
        public void DumpIncrementalSchemaDefinition() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            // create .df
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

            // create .df
            var dfPath = Path.Combine(TestFolder, "seqdata.df");
            File.WriteAllText(dfPath, "ADD SEQUENCE \"sequence1\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n\nADD SEQUENCE \"sequence2\"\n  INITIAL 0\n  INCREMENT 1\n  CYCLE-ON-LIMIT no\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                var dbPath = Path.Combine(TestFolder, "seqdata.db");
                dataAdmin.CreateCompilationDatabaseFromDf(dbPath, dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(dbPath).Equals(DatabaseBusyMode.NotBusy));

                var sequenceDataFilePath = Path.Combine(TestFolder, "dumpseqdata.d");

                // load seq
                File.WriteAllText(sequenceDataFilePath, "0 \"sequence1\" 20\n");
                dataAdmin.LoadSequenceData(dbPath, sequenceDataFilePath);

                // dump seq
                File.Delete(sequenceDataFilePath);
                dataAdmin.DumpSequenceData(dbPath, sequenceDataFilePath);

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

            // create .df
            var dfPath = Path.Combine(TestFolder, "data.df");
            File.WriteAllText(dfPath, "ADD TABLE \"table1\"\n  AREA \"Schema Area\"\n  DESCRIPTION \"table one\"\n  DUMP-NAME \"table1\"\n\nADD FIELD \"field1\" OF \"table1\" AS character \n  DESCRIPTION \"field one\"\n  FORMAT \"x(8)\"\n  INITIAL \"\"\n  POSITION 2\n  MAX-WIDTH 16\n  ORDER 10\n\nADD FIELD \"field2\" OF \"table1\" AS integer \n  DESCRIPTION \"field two\"\n  FORMAT \"9\"\n  INITIAL 0\n  POSITION 3\n  ORDER 20\n");

            using (var dataAdmin = new UoeDatabaseAdministrator(dlcPath)) {
                var dbPath = Path.Combine(TestFolder, "data.db");
                dataAdmin.CreateCompilationDatabaseFromDf(dbPath, dfPath);
                Assert.IsTrue(dataAdmin.GetBusyMode(dbPath).Equals(DatabaseBusyMode.NotBusy));

                var dataDirectory = Path.Combine(TestFolder, "data");
                Directory.CreateDirectory(dataDirectory);
                try {
                    var table1Path = Path.Combine(dataDirectory, "table1.d");
                    // load data
                    File.WriteAllText(table1Path, "\"value1\" 1\n\"value2\" 2\n");
                    dataAdmin.LoadData(dbPath, dataDirectory);

                    // dump seq
                    File.Delete(table1Path);
                    dataAdmin.DumpData(dbPath, dataDirectory);

                    Assert.IsTrue(File.Exists(table1Path));
                    var dataContent = File.ReadAllText(table1Path);
                    Assert.IsTrue(dataContent.Contains("\"value1\" 1"));
                    Assert.IsTrue(dataContent.Contains("\"value2\" 2"));

                } finally {
                    Directory.Delete(dataDirectory, true);
                }


            }

        }

    }
}
