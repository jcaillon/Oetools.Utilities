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

using System;
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

        [DataRow(@"-db data -ld logi -H hostname -S 1024", 1)]
        [DataRow(@"-db data -ld logi -1", 1)]
        [DataRow(@"-singleoption -db data -ld logi -1", 1)]
        [DataRow(@"-db data -ld logi -1 -db data2 -ld logi2 -H hostname -S 1024", 2)]
        [DataRow(@"-option value -db data -ld logi -1 -db data2 -ld logi2 -H hostname -S 1024 -db data3 -ld logi3 -H hostname -S 1025", 3)]
        [DataTestMethod]
        public void GetConnectionStrings_NumberOfCsReturned(string input, int nb) {
            var list = UoeConnectionString.GetConnectionStrings(input);
            Assert.AreEqual(nb, list.Count());
        }

        [DataRow(
            @"-option value -db data -ld logi -1 -db data2 -option2 value2 -singleoption -ld logi2 -H hostname -S 1024 -db data3 -ld logi3 -H hostname -S 1025",
            @"-db ""data"" -ld logi -1 -option value -db data2 -ld logi2 -H hostname -S 1024 -option2 value2 -singleoption -db data3 -ld logi3 -H hostname -S 1025")]
        [DataTestMethod]
        public void GetConnectionString(string input, string output) {
            output = output.Replace(@"""data""", "\"" + Path.Combine(Directory.GetCurrentDirectory(), "data.db") + "\"");
            Assert.AreEqual(output, UoeConnectionString.GetConnectionString(UoeConnectionString.GetConnectionStrings(input)));
        }

        [DataRow(@"-option value value")] // 2 values
        [DataRow(@"-db -option")] // -db without value
        [DataRow(@"-lb -option")]
        [DataRow(@"-H -option")]
        [DataRow(@"-S -option")]
        [DataRow(@"-ld logi -H hostname -S 1024")] // no -db
        [DataRow(@"-db data -ld logi -H hostname -S 1024 -1")] // -1 with -S
        [DataTestMethod]
        public void GetConnectionStrings_Except(string input) {
            Exception e = null;
            try {
                var list = UoeConnectionString.GetConnectionStrings(input);
                Assert.AreEqual(1, list.Count());
            } catch (UoeConnectionStringParseException ex) {
                e = ex;
            }
            Assert.IsNotNull(e);
        }

        [TestMethod]
        public void GetConnectionString_Multi_Single() {

            var cs = UoeConnectionString.NewMultiUserConnection(new UoeDatabaseLocation("data")).ToString();
            Assert.IsTrue(!cs.Contains("-1") && !cs.Contains("-H"), $"Should have multi user connection string without hostname: {cs.PrettyQuote()}.");

            cs = UoeConnectionString.NewSingleUserConnection(new UoeDatabaseLocation("data")).ToString();
            Assert.IsTrue(cs.Contains("-1") && cs.Contains("-db"), $"Should have single user connection string.");
        }
    }
}
