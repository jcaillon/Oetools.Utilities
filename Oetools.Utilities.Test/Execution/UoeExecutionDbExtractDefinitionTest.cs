#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionDbExtractTableAndSequenceListTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Execution {

    [TestClass]
    public class UoeExecutionDbExtractDefinitionTest {

        private static string _testFolder;

        protected static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeExecutionDbExtractDefinitionTest)));

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
        public void DumpNoDatabase() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }
            using (var exec = new UoeExecutionDbExtractDefinition(env)) {
                exec.ExecuteNoWait();
                exec.WaitForExit();
                Assert.IsFalse(exec.ExecutionHandledExceptions, "ExecutionHandledExceptions");
                Assert.IsFalse(exec.DatabaseConnectionFailed, "DbConnectionFailed");
                //Assert.AreEqual(0, exec.);
            }
            env.Dispose();
        }

        [TestMethod]
        public void Execute() {
            if (!GetEnvExecution(out UoeExecutionEnv env)) {
                return;
            }

            var base1Db = new UoeDatabaseLocation(Path.Combine(TestFolder, "base1.db"));

            if (!base1Db.Exists()) {
                var dfPath = Path.Combine(TestFolder, "base1.df");
                File.WriteAllBytes(dfPath, Resources.Resources.GetBytesFromResource("Database.complete.df"));
                using (var dbAdministrator = new UoeDatabaseAdministrator(env.DlcDirectoryPath)) {
                    dbAdministrator.CreateWithDf(base1Db, dfPath);
                }
            }

            env.DatabaseConnections = new []{ UoeDatabaseConnection.NewSingleUserConnection(base1Db) };
            using (var exec = new UoeExecutionDbExtractDefinition(env)) {
                exec.ExecuteNoWait();
                exec.WaitForExit();
                Assert.IsFalse(exec.ExecutionHandledExceptions, exec.ExecutionHandledExceptions ? exec.HandledExceptions[0].ToString() : "ok");
                Assert.IsFalse(exec.DatabaseConnectionFailed, "DbConnectionFailed");
                var db = exec.GetDatabases()[0];

                Assert.AreEqual( DatabaseBlockSize.S4096, db.BlockSize );
                Assert.AreEqual( "iso8859-1", db.Charset );
                Assert.AreEqual( "basic", db.Collation );
                Assert.AreEqual( "base1", db.LogicalName );
                Assert.AreEqual( "base1", db.PhysicalName );
                Assert.AreEqual( 2, db.Sequences.Count );
                Assert.AreEqual( true, db.Sequences[0].CycleOnLimit );
                Assert.AreEqual( 2, db.Sequences[0].Increment );
                Assert.AreEqual( 20, db.Sequences[0].Initial );
                Assert.AreEqual( 200, db.Sequences[0].Max );
                Assert.AreEqual( null, db.Sequences[0].Min );
                Assert.AreEqual( "theseq1", db.Sequences[0].Name );
                Assert.AreEqual( 2, db.Tables.Count );
                Assert.AreEqual( "Data Area", db.Tables[0].Area );
                Assert.AreEqual( 5575, db.Tables[0].Crc );
                Assert.AreEqual( "thedesc1", db.Tables[0].Description );
                Assert.AreEqual( "thedump1", db.Tables[0].DumpName );
                Assert.AreEqual( "theforeignname", db.Tables[0].Foreign );
                Assert.AreEqual( false, db.Tables[0].Frozen );
                Assert.AreEqual( false, db.Tables[0].Hidden );
                Assert.AreEqual( "thelabel1", db.Tables[0].Label );
                Assert.AreEqual( "T", db.Tables[0].LabelAttribute );
                Assert.AreEqual( "thetable1", db.Tables[0].Name );
                Assert.AreEqual( "thereplication1", db.Tables[0].Replication );
                Assert.AreEqual( "false", db.Tables[0].ValidationExpression );
                Assert.AreEqual( "not true", db.Tables[0].ValidationMessage );
                Assert.AreEqual( "R", db.Tables[0].ValidationMessageAttribute );
                Assert.AreEqual( 3, db.Tables[0].Fields.Count );
                Assert.AreEqual( true, db.Tables[0].Fields[0].CaseSensitive );
                Assert.AreEqual( null, db.Tables[0].Fields[0].ClobCharset );
                Assert.AreEqual( null, db.Tables[0].Fields[0].ClobCollation );
                Assert.AreEqual( 0, db.Tables[0].Fields[0].ClobType );
                Assert.AreEqual( "thecollabel1", db.Tables[0].Fields[0].ColumnLabel );
                Assert.AreEqual( "T", db.Tables[0].Fields[0].ColumnLabelAttribute );
                Assert.AreEqual( UoeDatabaseDataType.Character, db.Tables[0].Fields[0].DataType );
                Assert.AreEqual( 0, db.Tables[0].Fields[0].Decimals );
                Assert.AreEqual( "thedesc1", db.Tables[0].Fields[0].Description );
                Assert.AreEqual( 2, db.Tables[0].Fields[0].Extent );
                Assert.AreEqual( "x(10)", db.Tables[0].Fields[0].Format );
                Assert.AreEqual( "T", db.Tables[0].Fields[0].FormatAttribute );
                Assert.AreEqual( "thehelp1", db.Tables[0].Fields[0].Help );
                Assert.AreEqual( "T", db.Tables[0].Fields[0].HelpAttribute );
                Assert.AreEqual( "theinitial1", db.Tables[0].Fields[0].InitialValue );
                Assert.AreEqual( "T", db.Tables[0].Fields[0].InitialValueAttribute );
                Assert.AreEqual( "thelabel1", db.Tables[0].Fields[0].Label );
                Assert.AreEqual( "T", db.Tables[0].Fields[0].LabelAttribute );
                Assert.AreEqual( null, db.Tables[0].Fields[0].LobArea );
                Assert.AreEqual( 0, db.Tables[0].Fields[0].LobBytes );
                Assert.AreEqual( null, db.Tables[0].Fields[0].LobSize );
                Assert.AreEqual( true, db.Tables[0].Fields[0].Mandatory );
                Assert.AreEqual( "thefield1", db.Tables[0].Fields[0].Name );
                Assert.AreEqual( 10, db.Tables[0].Fields[0].Order );
                Assert.AreEqual( 2, db.Tables[0].Fields[0].Position );
                Assert.AreEqual( null, db.Tables[0].Fields[0].Triggers );
                Assert.AreEqual( 44, db.Tables[0].Fields[0].Width );
                Assert.AreEqual( false, db.Tables[0].Fields[1].CaseSensitive );
                Assert.AreEqual( null, db.Tables[0].Fields[1].ClobCharset );
                Assert.AreEqual( null, db.Tables[0].Fields[1].ClobCollation );
                Assert.AreEqual( 0, db.Tables[0].Fields[1].ClobType );
                Assert.AreEqual( null, db.Tables[0].Fields[1].ColumnLabel );
                Assert.AreEqual( null, db.Tables[0].Fields[1].ColumnLabelAttribute );
                Assert.AreEqual( UoeDatabaseDataType.Integer, db.Tables[0].Fields[1].DataType );
                Assert.AreEqual( 0, db.Tables[0].Fields[1].Decimals );
                Assert.AreEqual( "", db.Tables[0].Fields[1].Description );
                Assert.AreEqual( 0, db.Tables[0].Fields[1].Extent );
                Assert.AreEqual( "9999", db.Tables[0].Fields[1].Format );
                Assert.AreEqual( null, db.Tables[0].Fields[1].FormatAttribute );
                Assert.AreEqual( "", db.Tables[0].Fields[1].Help );
                Assert.AreEqual( "", db.Tables[0].Fields[1].HelpAttribute );
                Assert.AreEqual( "0", db.Tables[0].Fields[1].InitialValue );
                Assert.AreEqual( null, db.Tables[0].Fields[1].InitialValueAttribute );
                Assert.AreEqual( null, db.Tables[0].Fields[1].Label );
                Assert.AreEqual( null, db.Tables[0].Fields[1].LabelAttribute );
                Assert.AreEqual( null, db.Tables[0].Fields[1].LobArea );
                Assert.AreEqual( 0, db.Tables[0].Fields[1].LobBytes );
                Assert.AreEqual( null, db.Tables[0].Fields[1].LobSize );
                Assert.AreEqual( true, db.Tables[0].Fields[1].Mandatory );
                Assert.AreEqual( "thefield2", db.Tables[0].Fields[1].Name );
                Assert.AreEqual( 20, db.Tables[0].Fields[1].Order );
                Assert.AreEqual( 3, db.Tables[0].Fields[1].Position );
                Assert.AreEqual( 1, db.Tables[0].Fields[1].Triggers.Count);
                Assert.AreEqual( 4, db.Tables[0].Fields[1].Width );
                Assert.AreEqual( 3, db.Tables[0].Indexes.Count );
                Assert.AreEqual( true, db.Tables[0].Indexes[0].Active );
                Assert.AreEqual( "Index Area", db.Tables[0].Indexes[0].Area );
                Assert.AreEqual( 36396, db.Tables[0].Indexes[0].Crc );
                Assert.AreEqual( "thedesc1", db.Tables[0].Indexes[0].Description );
                Assert.AreEqual( "theindex1", db.Tables[0].Indexes[0].Name );
                Assert.AreEqual( 1, db.Tables[0].Indexes[0].Fields.Count );
                Assert.AreEqual( false, db.Tables[0].Indexes[0].Primary );
                Assert.AreEqual( false, db.Tables[0].Indexes[0].Unique );
                Assert.AreEqual( true, db.Tables[0].Indexes[0].Word );
                Assert.AreEqual( true, db.Tables[0].Indexes[1].Active );
                Assert.AreEqual( "Index Area", db.Tables[0].Indexes[1].Area );
                Assert.AreEqual( 16650, db.Tables[0].Indexes[1].Crc );
                Assert.AreEqual( "desc2", db.Tables[0].Indexes[1].Description );
                Assert.AreEqual( 1, db.Tables[0].Indexes[1].Fields.Count );
                Assert.AreEqual( false, db.Tables[0].Indexes[1].Fields[0].Abbreviate );
                Assert.AreEqual( true, db.Tables[0].Indexes[1].Fields[0].Ascending );
                Assert.AreEqual( "theindex2", db.Tables[0].Indexes[1].Name );
                Assert.AreEqual( true, db.Tables[0].Indexes[1].Primary );
                Assert.AreEqual( true, db.Tables[0].Indexes[1].Unique );
                Assert.AreEqual( false, db.Tables[0].Indexes[1].Word );
                Assert.AreEqual( 1, db.Tables[0].Triggers.Count );
                Assert.AreEqual( 0, db.Tables[0].Triggers[0].Crc );
                Assert.AreEqual( UoeDatabaseTriggerEvent.Create, db.Tables[0].Triggers[0].Event );
                Assert.AreEqual( true, db.Tables[0].Triggers[0].Overridable );
                Assert.AreEqual( "theproc1", db.Tables[0].Triggers[0].Procedure );
                Assert.AreEqual( "Data Area", db.Tables[1].Area );
                Assert.AreEqual( 94, db.Tables[1].Crc );
                Assert.AreEqual( "", db.Tables[1].Description );
                Assert.AreEqual( "thetable2", db.Tables[1].DumpName );
                Assert.AreEqual( 11, db.Tables[1].Fields.Count);
                Assert.AreEqual( null, db.Tables[1].Foreign );
                Assert.AreEqual( false, db.Tables[1].Frozen );
                Assert.AreEqual( false, db.Tables[1].Hidden );
                Assert.AreEqual( null, db.Tables[1].Indexes );
                Assert.AreEqual( null, db.Tables[1].Label );
                Assert.AreEqual( null, db.Tables[1].LabelAttribute );
                Assert.AreEqual( "thetable2", db.Tables[1].Name );
                Assert.AreEqual( null, db.Tables[1].Replication );
                Assert.AreEqual( null, db.Tables[1].Triggers );
                Assert.AreEqual( UoeDatabaseTableType.T, db.Tables[1].Type );
                Assert.AreEqual( null, db.Tables[1].ValidationExpression );
                Assert.AreEqual( "", db.Tables[1].ValidationMessage );
                Assert.AreEqual( null, db.Tables[1].ValidationMessageAttribute );

            }

            env.Dispose();
        }

        private bool GetEnvExecution(out UoeExecutionEnv env) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                env = null;
                return false;
            }
            env = new UoeExecutionEnv {
                DlcDirectoryPath = dlcPath
            };
            return true;
        }

    }
}
