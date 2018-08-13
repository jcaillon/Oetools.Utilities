#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ArchiveCabTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Cab;

namespace Oetools.Utilities.Test.Archive.Cab {
    
    [TestClass]
    public class ArchiveCabTest {

        private static string _testFolder;
        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ArchiveCabTest)));

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
        public void CreateCab_noCompression() {
            
            CabArchiver archiver = new CabArchiver();
            List<IFileToArchive> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.cab");
            listFiles.AddRange(TestHelper.GetPackageTestFilesList(TestFolder, "out2.cab"));
            
            TestHelper.CreateSourceFiles(listFiles);           
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);
            
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out.cab")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out2.cab")));
            
            // verify
            ListCab(listFiles);
        }

        private void ListCab(IEnumerable<IFileToArchive> listFiles) {
            IArchiver archiver = new CabArchiver();
            foreach (var groupedFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                foreach (var file in files) {
                    Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInArchive.Equals(file.RelativePathInArchive)));
                }
                Assert.AreEqual(groupedFiles.ToList().Count, files.Count);
            }
        }

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) {
            
        }
    }
}