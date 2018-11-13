#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (HttpFileServerArchiverTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Test.Archive.Xcode {

    [TestClass]
    public class XcodeArchiverTest : ArchiveTest {

        private static string _testFolder;
        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(XcodeArchiverTest)));

        [ClassInitialize]
        public static void Init(TestContext context) {
            Cleanup();
            Directory.CreateDirectory(TestFolder);
        }

        [ClassCleanup]
        public static void Cleanup() {
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }

        [TestMethod]
        public void Test() {
            var archiver = Archiver.NewXcodeArchiver();
            
            archiver.SetKey("progress");
            archiver.SetEncodeMode(true);
            
            var listFiles = GetPackageTestFilesList(TestFolder, Path.Combine(TestFolder, "archives", "test1"));
            listFiles.AddRange(GetPackageTestFilesList(TestFolder, Path.Combine(TestFolder, "archives", "test2")));
            
            archiver.OnProgress += ArchiverOnOnProgress;
            CreateArchive(archiver, listFiles);
            archiver.OnProgress -= ArchiverOnOnProgress;
        }
    }
}