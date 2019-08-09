#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoePfTokenizerTest.cs) is part of Oetools.Utilities.Test.
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
using DotUtilities.ParameterString;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Test {

    [TestClass]
    public class UoePfTokenizerTest {

        [DataTestMethod]
        [DataRow("-opt#comment\n#comment line\rvalue", true, "ocscsve", "-opt;value")]
        [DataRow("~t~r~n~055", true, "ve", "\t\r\n-")]
        [DataRow("-opt \"value with space\"", true, "osve", "-opt;value with space")]
        [DataRow("~~", true, "ve", "~")]
        [DataRow("~~", false, "ve", "~~")]
        [DataRow("\\\\", true, "ve", "\\\\")]
        [DataRow("\\\\", false, "ve", "\\")]
        [DataRow("-opt 'value with space'", true, "osve", "-opt;value with space")]
        [DataRow("-opt ~'value with space~'", true, "osvsvsve", "-opt;'value;with;space'")]
        [DataRow("-opt \\'value with space\\'", false, "osvsvsve", "-opt;'value;with;space'")]
        public void Parse(string input, bool isWindows, string tokenTypes, string csvExpected) {

            var types = new StringBuilder();
            var csv = new StringBuilder();
            var tokenizer = UoePfTokenizer.New(input, isWindows);
            while (tokenizer.MoveToNextToken()) {
                var token = tokenizer.PeekAtToken(0);
                switch (token) {
                    case ParameterStringTokenOption _:
                        types.Append("o");
                        csv.Append(token.Value).Append(';');
                        break;
                    case ParameterStringTokenValue _:
                        types.Append("v");
                        csv.Append(token.Value).Append(';');
                        break;
                    case ParameterStringTokenWhiteSpace _:
                        types.Append("s");
                        break;
                    case ParameterStringTokenComment _:
                        types.Append("c");
                        break;
                    default:
                        types.Append("e");
                        break;
                }
            }

            Assert.AreEqual(tokenTypes, types.ToString());
            Assert.AreEqual($"{csvExpected};", csv.ToString());
        }
    }
}
