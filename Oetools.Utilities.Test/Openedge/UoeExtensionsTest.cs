#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExtensionsTest.cs) is part of Oetools.Utilities.Test.
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Test.Openedge {

    [TestClass]
    public class UoeExtensionsTest {

        [TestMethod]
        [DataRow(null, @"?")]
        [DataRow(@"", @"""""")]
        [DataRow("mot", @"""mot""")]
        [DataRow("mot\ndeux", @"""mot~ndeux""")]
        [DataRow("mot\"\ndeux", @"""mot""""~ndeux""")]
        [DataRow("mot~cool\"\nde{ux\r\t", @"""mot~~cool""""~nde~{ux~r~t""")]
        public void ProStringify(string input, string expected) {
            Assert.AreEqual(expected, input.ProStringify());
            Assert.AreEqual(input, expected.ProUnStringify());
        }

        [TestMethod]
        [DataRow(@"", @"")]
        [DataRow(@"?", null)]
        [DataRow("mot", @"mot")]
        [DataRow(@"mot~ndeux", "mot\ndeux")]
        [DataRow(@"mot~tdeux", "mot\tdeux")]
        public void ProUnescapeSpecialChar(string input, string expected) {
            Assert.AreEqual(expected, input.ProUnescapeSpecialChar());
        }

        [TestMethod]
        [DataRow(null, @"""""")]
        [DataRow(@"", @"""""")]
        [DataRow("mot", @"""mot""")]
        [DataRow("mot\ndeux", @"""mot~~ndeux""")]
        [DataRow("mot\"\ndeux", @"""mot""""~~ndeux""")]
        [DataRow("mot~cool\"\nde{ux\r\t", @"""mot~~~~cool""""~~nde~~{ux~~r~~t""")]
        public void ProPreProcStringify(string input, string expected) {
            Assert.AreEqual(expected, input.ProPreProcStringify());
        }
    }
}
