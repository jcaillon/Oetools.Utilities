#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (StringExtensionsTest.cs) is part of Oetools.Utilities.Test.
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
using System.ComponentModel;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Test.Lib.Extensions {

    [TestClass]
    public class ExtensionTest {

        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ExtensionTest)));

        [TestMethod]
        [DataRow(null, null)]
        [DataRow(@"", @"")]
        [DataRow(@"""", @"""")]
        [DataRow(@"""zef", @"""zef")]
        [DataRow(@"""zef""", @"zef")]
        public void StripQuotes_Test(string input, string expected) {
            Assert.AreEqual(expected, input.StripQuotes());
        }

        [TestMethod]
        [DataRow(@"    ", @"")]
        [DataRow(null, @"")]
        [DataRow(" motend     \t", @" motend")]
        public void TrimEnd_IsOk(string input, string expected) {
            Assert.AreEqual(expected, new StringBuilder(input).TrimEnd().ToString());
        }

        [TestMethod]
        [DataRow(@"a{{b{{c}}}}d", "abcd")]
        [DataRow(@"0{{1}}{{2}}{{3}}", "0123")]
        [DataRow(@"{{0}}{{1}}{{2}}{{3}}", "0123")]
        [DataRow(@"0{{1}}0{{2}}0{{3}}", "010203")]
        [DataRow(@"0{{1{{2{{3}}}}}}", "0123")]
        [DataRow(@"{{}}", "")]
        [DataRow(@"{{bla}}", "bla")]
        public void ReplacePlaceHolders_Test(string pattern, string expected) {
            Assert.AreEqual(expected, pattern.ReplacePlaceHolders(s => s), pattern);
        }

        [TestMethod]
        [DataRow(@"^^^zf^^^ez$$$$$$f", "^^^", "$$$", "zfezf")]
        [DataRow(@"<TAG>hello</TAG> <TAG>there</TAG>", "<TAG>", "</TAG>", "hello there")]
        public void ReplacePlaceHolders_Test_placeHolderOpenClose(string pattern, string open, string close, string expected) {
            Assert.AreEqual(expected, pattern.ReplacePlaceHolders(s => s, open, close), pattern);
        }

        [TestMethod]
        [DataRow(@"0<1><2><3>")]
        [DataRow(@"0<1>0<2>0<3>")]
        [DataRow(@"0<1<2<3>>>")]
        [DataRow(@"<>")]
        [DataRow(@"<bla>")]
        public void ReplacePlaceHolders_Test_null_doesNotReplaceAnything(string pattern) {
            Assert.AreEqual(pattern, pattern.ReplacePlaceHolders(s => null), pattern);
        }

        [TestMethod]
        [DataRow(@"{{}}")]
        public void ReplacePlaceHolders_Test_expect_exceptions(string pattern) {
            // expect Invalid symbol > found at column 1 (no corresponding <)
            Assert.ThrowsException<Exception>(() => pattern.ReplacePlaceHolders(s => "{{}}"));
        }

        [TestMethod]
        [DataRow(@"hello{{var1}}nice", "a{{fe}}fef")]
        [DataRow(@"hello{{var1}}nice", "{{var2}}")]
        [DataRow(@"hello{{var1}}nice", "fefef}}")]
        public void ReplacePlaceHolders_Test_replace_with_placeholders_expect_exception(string pattern, string replacement) {
            // expect Invalid symbol }} found at column 1 (no corresponding {{)
            Assert.ThrowsException<Exception>(() => pattern.ReplacePlaceHolders(s => replacement));
        }

        [TestMethod]
        [DataRow(@"efzee{{", DisplayName = "badly formatted input string, it should have been validated first, we expect exception")]
        [DataRow(@"{{bla", DisplayName = "unclosed place holder")]
        public void ReplacePlaceHolders_Test_expect_exceptions2(string pattern) {
            Assert.ThrowsException<Exception>(() => pattern.ReplacePlaceHolders(s => s));
        }

        [TestMethod]
        [DataRow(@"<<zf<<ez>>f>>", "<<", ">>", 1, false, DisplayName = "too much depth")]
        [DataRow(@"<<zf<<ez>>f", "<<", ">>", 0, false, DisplayName = "unbalanced open/close")]
        [DataRow(@"<<zf<<ez>>f>>bou>>", "<<", ">>", 2, false, DisplayName = "too many closing tags")]
        [DataRow(@"^zf^ez$f$", "^", "$", 0,  true)]
        [DataRow(@"^zf^ez$f$", "^", "$", 2,  true)]
        [DataRow(@"^^^zf^^^ez$$$f$$$", "^^^", "$$$", 0,  true)]
        public void HasValidPlaceHolders_Test(string pattern, string open, string close, int maxdepth, bool expectOk) {
            if (!expectOk) {
                Assert.ThrowsException<Exception>(() => pattern.ValidatePlaceHolders(open, close, maxdepth));
            } else {
                pattern.ValidatePlaceHolders(open, close, maxdepth);
            }
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
        [DataRow(@"https:\\user:pwd@localhost:666\my\path", "user", "pwd", "localhost", 666, true)]
        [DataRow(@"http://user:pwd@localhost:666", "user", "pwd", "localhost", 666, true)]
        [DataRow(@"http://user@localhost:666\", "user", null, "localhost", 666, true)]
        [DataRow(@"http://localhost:666", null, null, "localhost", 666, true)]
        [DataRow(@"http://localhost:666/", null, null, "localhost", 666, true)]
        [DataRow(@"http://localhost/", null, null, "localhost", 0, true)]
        [DataRow(@"httpa://localhost/", null, null, null, 0, false)]
        public void ParseHttpAddress(string input, string euserName, string epassWord, string ehost, int eport, bool ok) {
            Assert.AreEqual(ok, input.ParseHttpAddress(out var name, out var passWord, out var host, out var port));
            Assert.AreEqual(euserName, name);
            Assert.AreEqual(epassWord, passWord);
            Assert.AreEqual(ehost, host);
            Assert.AreEqual(eport, port);
        }

        [TestMethod]
        [DataRow(@"    ", @"")]
        [DataRow(null, @"")]
        [DataRow(" mot     \t deux \n\r\n\rtrois     end     \t", @"mot deux trois end")]
        [DataRow(" mot     \t \"deux \t \t\t trois\"     end     \t", "mot \"deux \t \t\t trois\" end")]
        public void CompactWhitespaces_IsOk(string input, string expected) {
            Assert.AreEqual(expected, input.CliCompactWhitespaces());
        }

        [TestMethod]
        [DataRow(null, true, @"""""")]
        [DataRow(@"", true, @"""""")]
        [DataRow("mot", true, @"mot", DisplayName = "does not quote if unnecessary")]
        [DataRow("mot", false, @"mot")]
        [DataRow("mot deux", true, @"""mot deux""", DisplayName = "necessary quotes because of the space")]
        [DataRow("mot deux", false, @"""mot deux""")]
        [DataRow("mot\"deux\"", true, @"mot\""deux\""", DisplayName = "not surrounding quotes, escaped quote with backslash")]
        [DataRow("mot\"deux\"", false, @"mot\""deux\""")]
        [DataRow("mot \"deux\"", true, @"""mot \""deux\""""")]
        [DataRow("mot \"deux\"", false, @"""mot \""deux\""""")]
        [DataRow("\"mot \"deux\"\"", true, @"""mot \""deux\""""", DisplayName = "if the string is initially quoted we check the inside to see if the quotes are necessary")]
        [DataRow("\"mot \"deux\"\"", false, @"""mot \""deux\""""")]
        [DataRow("\"m\"ot\"", true, @"m\""ot", DisplayName = "here, the initial quotes are useless")]
        [DataRow("\"m\"ot\"", false, @"m\""ot")]
        [DataRow("mot\\deux", true, @"mot\deux", DisplayName = "on windows no need to escape a \\ in the middle")]
        [DataRow("mot\\deux", false, @"mot\\deux", DisplayName = "on linux yes")]
        [DataRow("mot \\deux", true, @"""mot \deux""")]
        [DataRow("mot \\deux", false, @"""mot \\deux""")]
        [DataRow("mot \\\"deux", true, @"""mot \\\""deux""", DisplayName = "special case 1 on windows, need 3 backslash here")]
        [DataRow("mot\\", true, @"mot\", DisplayName = "special case 2 on windows no need to escape a final backslash if no surrounding quotes")]
        [DataRow("mot deux\\", true, @"""mot deux\\""", DisplayName = "special case 3 on windows, need to escape the last backslash because of the surrounding quotes")]
        public void ToCliArg(string input, bool isWindows, string expected) {
            Assert.AreEqual(expected, input.ToCliArg(isWindows));
        }

        [TestMethod]
        [DataRow(@"arg1", @"""my value""", @"arg1 ""my value""")]
        public void EscapedCliArgs_Test(string input, string input2, string expected) {
            Assert.AreEqual(expected, new [] { input, input2 }.EscapedCliArgs());
        }

        [TestMethod]
        [DataRow(null, @"?")]
        [DataRow(@"", @"""""")]
        [DataRow("mot", @"""mot""")]
        [DataRow("mot\ndeux", @"""mot~ndeux""")]
        [DataRow("mot\"\ndeux", @"""mot""""~ndeux""")]
        [DataRow("mot~cool\"\nde{ux\r\t", @"""mot~~cool""""~nde~{ux~r~t""")]
        public void ProQuoter_ProUnescape_Test(string input, string expected) {
            Assert.AreEqual(expected, input.ProStringify());
            Assert.AreEqual(input, expected.ProUnescapeString());
        }

        [TestMethod]
        [DataRow(null, @"""""")]
        [DataRow(@"", @"""""")]
        [DataRow("mot", @"""mot""")]
        [DataRow("mot\ndeux", @"""mot~~ndeux""")]
        [DataRow("mot\"\ndeux", @"""mot""""~~ndeux""")]
        [DataRow("mot~cool\"\nde{ux\r\t", @"""mot~~~~cool""""~~nde~~{ux~~r~~t""")]
        public void PreProcQuoter_Test(string input, string expected) {
            Assert.AreEqual(expected, input.ProPreProcStringify());
        }

        [TestMethod]
        [DataRow(null, @"""""")]
        [DataRow(@"", @"""""")]
        [DataRow("mot", @"mot")]
        [DataRow("mot deux", @"""mot deux""")]
        [DataRow("mot\"deux", @"mot""""deux")]
        [DataRow("mot cool\"", @"""mot cool""""""")]
        public void Quoter(string input, string expected) {
            Assert.AreEqual(expected, input.Quoter());
        }

        [TestMethod]
        public void ToCleanQuoterArgs() {
            var pf1 = Path.Combine(TestFolder, "1.pf");
            var pf2 = Path.Combine(TestFolder, "2.pf");
            var pf3 = Path.Combine(TestFolder, "3.pf");
            File.WriteAllText(pf1, "-pf " + pf2.Quoter());
            File.WriteAllText(pf2, "-db \"base1 allo\"    -H hostname     -S 1024");
            File.WriteAllText(pf3, "-db base2     \n-H hostname\n# ignore this line!\n    -S 1025");
            Assert.AreEqual("-db \"base1 allo\" -H hostname -S 1024 -db base2 -H hostname -S 1025", ("-pf " + pf1.Quoter() + " -pf " + pf3.Quoter()).ToCleanQuoterArgs());
        }


        [TestMethod]
        [DataRow(@"-opt""""val arg", true, @"-opt\""val arg", DisplayName = "")]
        [DataRow(@"-opt""""val arg", false, @"-opt\""val arg", DisplayName = "")]
        [DataRow(@"-opt""val arg", true, @"""-optval arg""", DisplayName = "")]
        [DataRow(@"-opt""val arg", false, @"""-optval arg""", DisplayName = "")]
        [DataRow(@"-opt:"" val arg""", true, @"""-opt: val arg""", DisplayName = "")]
        [DataRow(@"-opt:"" val arg""", false, @"""-opt: val arg""", DisplayName = "")]
        [DataRow(@"""-opt """"val""""""", true, @"""-opt \""val\""""")]
        [DataRow(@"""-opt """"val""""""", false, @"""-opt \""val\""""")]
        public void ToCleanCliArgs(string input, bool isWindows, string expected) {
            Assert.AreEqual(expected, input.ToCleanCliArgs(isWindows));
        }
    }
}
