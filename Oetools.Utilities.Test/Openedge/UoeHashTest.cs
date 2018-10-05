#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeHashTest.cs) is part of Oetools.Utilities.Test.
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
    public class UoeHashTest {
        [DataTestMethod]
        [DataRow(new byte[] { }, "pjqtudckibycRKbj")]
        [DataRow(new byte[] { 35 }, "ddlneQRnljrbllpn")]
        [DataRow(new byte[] { 231, 16 }, "aiHULcdddldlaAil")]
        [DataRow(new byte[] { 124, 136, 60, 231, 52 }, "bkibllkLdqlpeIol")]
        [DataRow(new byte[] { 232, 114, 209, 52, 89, 51, 240, 255, 4, 23, 9, 245, 119, 172, 206, 201 }, "EbiagabdXlkhDcfi")]
        [DataRow(new byte[] { 103, 17, 250, 188, 192, 253, 127, 137, 47, 12, 103, 86, 128, 145, 194, 249, 143, 252, 17, 253, 136, 250, 45, 143, 101, 240, 255, 6, 211, 203 }, "wciikdXkfitbqjVt")]
        [DataRow(new byte[] { 202, 230, 113, 28, 116, 230, 141, 140, 65, 6, 65, 216, 115, 240, 74, 141, 209, 244, 167, 229, 100, 53, 37, 150, 166, 25, 230, 251, 221, 186, 98, 22, 219, 231, 116, 6, 106, 151, 59, 136, 41, 253, 98, 66, 120, 205, 5 }, "klintbaDcphabjad")]
        [DataRow(new byte[] { 154, 105, 199, 111, 170, 250, 21, 211, 6, 83, 14, 54, 238, 85, 34, 229, 255, 134, 44, 246, 220, 51, 179, 206, 125, 112, 204, 63, 166, 230, 68, 92, 88, 220, 149, 92, 192, 98, 186, 97, 124, 128, 100, 3, 2, 55, 93, 138, 26, 227, 126, 7, 168, 66, 120, 163, 59, 4, 239, 196, 175, 152 }, "wuphhbcPnhjbsadn")]
        [DataRow(new byte[] { 188, 248, 133, 145, 174, 87, 83, 249, 96, 118, 133, 217, 169, 194, 45, 157, 120, 201, 239, 146, 141, 12, 219, 72, 106, 149, 90, 187, 39, 84, 196, 107, 33, 118, 143, 140, 175, 31, 254, 235, 168, 248, 67, 128, 235, 162, 175, 37, 135, 114, 88, 135, 218, 140, 109, 157, 173, 235, 182, 251, 216, 21, 161, 247, 226, 62, 135 }, "fTljlTnGcjabbnni")]
        [DataRow(new byte[] { 123, 99, 208, 57, 98, 34, 201, 36, 115, 85, 214, 197, 231, 158, 87, 147, 156, 23, 103, 199, 85, 245, 21, 127, 212, 77, 61, 181, 105, 44, 41, 5, 196, 140, 195, 134, 213, 208, 34, 103, 135, 229, 137, 170, 67, 192, 177, 156, 208, 41, 229, 13, 227, 150, 65, 136, 17, 150, 104, 112, 187, 153, 108, 216, 104, 39, 76, 216, 192, 221, 31, 114, 69, 184, 137, 162, 216, 152, 92, 232, 255, 45, 152, 27, 48, 51, 160, 95 }, "ddlaaktdtYlaHjTk")]
        public void Test(byte[] input, string output) {
            Assert.AreEqual(output, UoeHash.Hash(input));
        }
    }
}