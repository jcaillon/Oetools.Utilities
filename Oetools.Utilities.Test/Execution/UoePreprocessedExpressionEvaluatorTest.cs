#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoePreprocessExpressionEvaluatorTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Openedge.Execution;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Test.Execution {

    [TestClass]
    public class UoePreprocessedExpressionEvaluatorTest {

        [DataTestMethod]
        [DataRow("1", true)]
        [DataRow("0", false)]
        [DataRow(" true  ", true)]
        [DataRow("  false   ", false)]
        [DataRow(" 1 =   1  ", true)]
        [DataRow(" 1 =   3  ", false)]
        [DataRow("\"not empty\"", true)]
        [DataRow("\"\"", false)]
        [DataRow("\"test\" MATCHES \"*te*\"", true)]
        [DataRow("\"test\" BEGINS \"te\"", true)]
        public void Istrue(string expression, bool expectedEvaluation) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            using (var exp = new UoePreprocessedExpressionEvaluator(dlcPath)) {
                Assert.AreEqual(expectedEvaluation, exp.IsTrue(expression));
            }
        }

        [DataTestMethod]
        [DataRow("?")]
        public void Istrue_exception(string expression) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            using (var exp = new UoePreprocessedExpressionEvaluator(dlcPath)) {
                Assert.ThrowsException<UoePreprocessedExpressionEvaluationException>(() => exp.IsTrue(expression));
            }
        }
    }
}
