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

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Test.Lib.Extensions {
    
    [TestClass]
    public class ExtensionTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(UtilsTest)));

        [TestMethod]
        [DataRow(@"    ", @"")]
        [DataRow(null, @"")]
        [DataRow(" motend     \t", @" motend")]
        public void TrimEnd_IsOk(string input, string expected) {
            Assert.AreEqual(expected, new StringBuilder(input).TrimEnd().ToString());
        }
        
        [TestMethod]
        [DataRow(@"file.xml", @"*.xml", true)]
        [DataRow(@"file.xml", @"*.fk,*.xml", true)]
        [DataRow(@"file.derp", @"*.fk,*.xml", false)]
        [DataRow(@"path/file.xml", @"*.fk,*.xml", true)]
        [DataRow(@"path2\file.xml", @"*.fk,*.xml", true)]
        [DataRow(@"path2\file", @"*.fk,*.xml", false)]
        [DataRow(@"file.fk", @"*.fk,*.xml", true)]
        [DataRow(@"", @"*.fk,*.xml", false)]
        [DataRow(@"file", @"", false)]
        [DataRow(null, null, false)]
        public void TestFileAgainstListOfExtensions_Test(string source, string pattern, bool expected) {
            Assert.AreEqual(expected, source.TestFileNameAgainstListOfPatterns(pattern));
        }
        
        [TestMethod]
        [DataRow(@"c/folder/file.ext", @"fack", false)]
        [DataRow(@"c/folder/file.ext", @"folder", false)]
        [DataRow(@"c/folder/file.ext", @"**folder**", true)]
        [DataRow(@"c/folder/file.ext", @"**/file", false)]
        [DataRow(@"c/folder/file.ext", @"**/file.ext", true)]
        [DataRow(@"c\folder\file.ext", @"**\file.ext", true)]
        [DataRow(@"c\folder\file.ext", @"**\file.*", true)]
        [DataRow(@"c\folder\file.ext", @"**\??le.*", true)]
        [DataRow(@"c\folder\file.ext", @"*\file.ext", false)]
        [DataRow(@"c/folder\file.ext", @"d/**", false)]
        [DataRow(@"c/folder\file.ext", @"?/**", true)]
        [DataRow(@"c/folder\file.ext", @"c/**", true)]
        [DataRow(@"c/folder\file.ext", @"c/**/*.ext", true)]
        [DataRow(@"c/folder\file.ext", @"c/*/*", true)]
        [DataRow(@"c/folder\file.ext", @"c/*/*/*", false)]
        [DataRow(@"c/folder/file.ext", @"c\folder/**", true)]
        [DataRow(@"c\folder/file.ext", @"c/folder/**/file.ext", true)]
        [DataRow(@"c\folder/file.ext", @"**\folder/**/file**", true)]
        [DataRow(@"c\folder/file.ext", @"**/*/**/*", true)]
        [DataRow(@"file.ext", @"**/*/**/*", false)]
        [DataRow(@"file.ext", null, false)]
        [DataRow(@"file.ext", @"", false)]
        public void TestAgainstListOfPatterns_Test(string source, string pattern, bool expected) {
            Assert.AreEqual(expected, source.TestAgainstListOfPatterns(pattern));
        }
        
        [TestMethod]
        [DataRow(null, false)]
        [DataRow(@"<00000>", true)]
        [DataRow(@"<0<1>0>", true)]
        [DataRow(@"<0<1111>0000<22222>0>", true)]
        [DataRow(@"<<<><>>>", true)]
        [DataRow(@"000>><<", false)]
        [DataRow(@"<<0000>", false)]
        [DataRow(@"<000>>", false)]
        [DataRow(@"000>>", false)]
        [DataRow(@"<0<1<2<3>>>>0>>", false)]
        public void IsPathWildCardValid_Test(string pattern, bool expected) {
            Assert.AreEqual(expected, pattern.ArePathWildCardMatchesValid());
        }
        
        [TestMethod]
        [DataRow(@"ftps:\\user:pwd@localhost:666\my\path", "ftps://user:pwd@localhost:666", "user", "pwd", "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://user:pwd@localhost:666/my/path", "ftp://user:pwd@localhost:666", "user", "pwd", "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://user@localhost:666/my/path", "ftp://user@localhost:666", "user", null, "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://localhost:666/my/path", "ftp://localhost:666", null, null, "localhost", 666, "/my/path", true)]
        [DataRow(@"ftp://localhost:666/", "ftp://localhost:666", null, null, "localhost", 666, "/", true)]
        [DataRow(@"ftp://localhost/", "ftp://localhost", null, null, "localhost", 0, "/", true)]
        [DataRow(@"ftpa://localhost/", null, null, null, null, 0, null, false)]
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
        [DataRow(@"    ", @"")]
        [DataRow(null, @"")]
        [DataRow(" mot     \t deux \n\r\n\rtrois     end     \t", @"mot deux trois end")]
        public void CompactWhitespaces_IsOk(string input, string expected) {
            Assert.AreEqual(expected, input.CompactWhitespaces());
        }
    }
}