#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeProMessageText.cs) is part of Oetools.Utilities.Test.
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

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Test {

    [TestClass]
    public class UoeProMessageTest {

        [TestMethod]
        [DataRow(49)] // This Technical Support Knowled...
        [DataRow(1)] // The -nb parameter is followed by a number that sp...
        [DataRow(1964)] // COBOL binary or COMP var...
        [DataRow(612)] // PROGRESS tried to read or write the
        public void GetOpenedgeErrorDetailedMessage_Test(int errorNumber) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            var res = UoeProMessage.GetProMessage(dlcPath, errorNumber);
            Debug.WriteLine(res);
            Assert.IsNotNull(res, "null");
        }
    }
}
