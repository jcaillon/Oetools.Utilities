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
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Database;

namespace Oetools.Utilities.Test.Openedge.Database {

    [TestClass]
    public class UoeConnectionStringTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UoeConnectionStringTest)));

        //[DataRow(@"", true)]
        //[DataRow(null, true)]
        //[DataRow("123456789012345678901234567890123", true, DisplayName = "33 chars is too much")]
        //[DataRow(@"azée", true, DisplayName = "contains invalid accent char")]
        //[DataRow(@"zeffez zezffe", true, DisplayName = "spaces")]
        //[DataRow(@"0ezzef", true, DisplayName = "first should be a letter")]
        //[DataRow(@"az_-zefze", false, DisplayName = "ok")]
        //[DataTestMethod]
        //public void ValidateLogicalName_Test(string input, bool exception) {
        //    if (exception) {
        //        Assert.ThrowsException<UoeDatabaseException>(() => UoeDatabase.ValidateLogicalName(input));
        //    } else {
        //        UoeDatabase.ValidateLogicalName(input);
        //    }
        //}
    }
}
