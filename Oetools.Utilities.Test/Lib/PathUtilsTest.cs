#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (PathUtilsTest.cs) is part of Oetools.Utilities.Test.
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Lib {
    
    [TestClass]
    public class PathUtilsTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(PathUtilsTest)));
                     
        [ClassInitialize]
        public static void Init(TestContext context) {
            Cleanup();
            Utils.CreateDirectoryIfNeeded(TestFolder);
        }


        [ClassCleanup]
        public static void Cleanup() {
            Utils.DeleteDirectoryIfExists(TestFolder, true);
        }
        
        [TestMethod]
        public void EnumerateFolders_Test() {
            Directory.CreateDirectory(Path.Combine(TestFolder, "test1"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2", "subtest2"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2", "subtest2", "end2"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3", "subtest3"));
            
            var dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test1_hidden"));
            dirInfo.Attributes |= FileAttributes.Hidden;
            dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test2", "test2_hidden"));
            dirInfo.Attributes |= FileAttributes.Hidden;

            var list = Utils.EnumerateAllFolders(TestFolder).ToList();
            Assert.AreEqual(8, list.Count);
            
            list = Utils.EnumerateAllFolders(TestFolder, excludeHidden: true).ToList();
            Assert.AreEqual(6, list.Count);
            Assert.AreEqual(false, list.Any(s => s.Contains("_hidden")));
            
            list = Utils.EnumerateAllFolders(TestFolder, SearchOption.TopDirectoryOnly).ToList();
            Assert.AreEqual(4, list.Count);
            
            list = Utils.EnumerateAllFolders(TestFolder, SearchOption.AllDirectories, new List<string> {
                @".*_hid.*",
                @"test3"
            }).ToList();
            Assert.AreEqual(4, list.Count);
        }
        
        [DataTestMethod]
        [DataRow(@"C:\windows(bla|bla)\<pozdzek!>", @"C:\windows")]
        [DataRow(@"C:\^$windows(bla|bla)\<pozdzek!>", @"C:\")]
        public void GetLongestValidDirectory_Test(string input, string expected) {
            if (Utils.IsRuntimeWindowsPlatform) {
                Assert.AreEqual(expected, Utils.GetLongestValidDirectory(input));
            }
        }
        
        [TestMethod]
        public void EnumerateFiles_Test() {
            File.WriteAllText(Path.Combine(TestFolder, "file1"), "");
            Directory.CreateDirectory(Path.Combine(TestFolder, "test1"));
            File.WriteAllText(Path.Combine(TestFolder, "test1", "file2"), "");
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2"));
            File.WriteAllText(Path.Combine(TestFolder, "test2", "file3"), "");
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2", "subtest2"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test2", "subtest2", "end2"));
            File.WriteAllText(Path.Combine(TestFolder, "test2", "subtest2", "end2", "file4"), "");
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3"));
            Directory.CreateDirectory(Path.Combine(TestFolder, "test3", "subtest3"));
            File.WriteAllText(Path.Combine(TestFolder, "test3", "subtest3", "file5"), "");
            
            var dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test1_hidden"));
            File.WriteAllText(Path.Combine(TestFolder, "test1_hidden", "file6"), "");
            dirInfo.Attributes |= FileAttributes.Hidden;
            dirInfo = Directory.CreateDirectory(Path.Combine(TestFolder, "test2", "test2_hidden"));
            File.WriteAllText(Path.Combine(TestFolder, "test2", "test2_hidden", "file7"), "");
            dirInfo.Attributes |= FileAttributes.Hidden;
            
            var list = Utils.EnumerateAllFiles(TestFolder).ToList();
            Assert.AreEqual(7, list.Count);
            
            list = Utils.EnumerateAllFiles(TestFolder, SearchOption.TopDirectoryOnly).ToList();
            Assert.AreEqual(1, list.Count);
            
            list = Utils.EnumerateAllFiles(TestFolder, excludeHiddenFolders: true).ToList();
            Assert.AreEqual(5, list.Count);
            
            list = Utils.EnumerateAllFiles(TestFolder, SearchOption.AllDirectories , new List<string> {
                @"file1", // exclude file
                @".*[\\\/]test2[\\\/].*" // exclude folder
            }).ToList();
            Assert.AreEqual(3, list.Count);
            
            
        }
    }
}