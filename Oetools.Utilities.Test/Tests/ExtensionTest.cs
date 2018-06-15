#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UtilsTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Tests {
    
    [TestClass]
    public class ExtensionTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UtilsTest)));

        [TestMethod]
        [DataRow(@"ftps:\\user:pwd@localhost:666\my\path", "ftp://user:pwd@localhost:666", "user", "pwd", "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://user:pwd@localhost:666/my/path", "ftp://user:pwd@localhost:666", "user", "pwd", "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://user@localhost:666/my/path", "ftp://user:pwd@localhost:666", "user", "", "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://localhost:666/my/path", "ftp://user:pwd@localhost:666", null, null, "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://localhost:666/", "ftp://user:pwd@localhost:666", null, null, "localhost", 666, "/", true)]
        [DataRow(@"ftp://localhost/", "ftp://user:pwd@localhost:666", null, null, "localhost", 0, "/", true)]
        [DataRow(@"ftpa://localhost/", null, null, null, null, 0, "/", false)]
        public void ParseFtpAddress_IsOk(string input, string eftpBaseUri, string euserName, string epassWord, string ehost, int eport, string erelativePath, bool ok) {
            Assert.AreEqual(ok, input.ParseFtpAddress(out var ftpBaseUri, out var name, out var passWord, out var host, out var port, out var relativePath));
            Assert.AreEqual(eftpBaseUri, ftpBaseUri);
            Assert.AreEqual(euserName, name);
            Assert.AreEqual(epassWord, passWord);
            Assert.AreEqual(ehost, host);
            Assert.AreEqual(eport, port);
            Assert.AreEqual(erelativePath, relativePath);
        }
        
        [TestMethod]
        [DataRow(@"    ", @" ")]
        [DataRow(null, @"")]
        [DataRow(" mot     \t deux \n\r\n\rtrois     end     \t", @"mot deux trois end")]
        public void CompactWhitespaces_IsOk(string input, string expected) {
            Assert.AreEqual(expected, input.CompactWhitespaces());
        }
    }
}