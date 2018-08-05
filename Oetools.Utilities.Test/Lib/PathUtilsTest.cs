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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class PathUtilsTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(PathUtilsTest)));
                     
        [TestMethod]
        public void ListAllFoldersFromBaseDirectory_Test() {
            Directory.CreateDirectory(Path.Combine(TestFolder, "test1"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3", "test4"));
            var dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test1_hidden"));
            dirInfo.Attributes |= FileAttributes.Hidden;
            dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test2_hidden"));
            dirInfo.Attributes |= FileAttributes.Hidden;

            var list = Utils.ListAllFoldersFromBaseDirectory(TestFolder);

            Assert.AreEqual(4, list.Count);
            Assert.IsFalse(list.ToList().Exists(s => s.Contains("_hidden")));
            
            list = Utils.ListAllFoldersFromBaseDirectory(TestFolder, false);

            Assert.AreEqual(6, list.Count);
            Assert.IsTrue(list.ToList().Exists(s => s.Contains("_hidden")));
        }
    }
}