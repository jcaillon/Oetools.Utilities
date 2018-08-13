#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ArchiveProlibTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Archive.Prolib;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Archive.Prolib {
    [TestClass]
    public class ArchiveProlibTest {
        private static string _testFolder;
        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ArchiveProlibTest)));

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
        public void Prolib_StandardOutput_ForError() {
            if (!TestHelper.GetProlibPath(out string prolib)) {
                return;
            }

            var process = new ProcessIo(prolib);
            process.Execute("bad command");
            Assert.AreNotEqual(0, process.ExitCode);
            Assert.IsTrue(process.ErrorOutputArray.Count == 0);
            Assert.IsTrue(process.StandardOutputArray.Count > 0);
        }


        [TestMethod]
        public void CreatePl() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }

            ProlibArchiver archiver = new ProlibArchiver(dlcPath);
            List<IFileToArchive> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.pl");
            listFiles.AddRange(TestHelper.GetPackageTestFilesList(TestFolder, "out2.pl"));
            
            TestHelper.CreateSourceFiles(listFiles);
            
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out.pl")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out2.pl")));

            // verify
            ListPl(dlcPath, listFiles);
            
            // delete files
            DeleteFilesInPl(dlcPath, listFiles);
        }


        private void ListPl(string dlcPath, List<IFileToArchive> listFiles) {
            IArchiver archiver = new ProlibArchiver(dlcPath);
            foreach (var groupedFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                foreach (var file in files) {
                    Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInArchive.Equals(file.RelativePathInArchive)));
                }

                Assert.AreEqual(groupedFiles.ToList().Count, files.Count);
            }
        }
        
        
        private void DeleteFilesInPl(string dlcPath, List<IFileToArchive> listFiles) {
            IArchiver archiver = new ProlibArchiveDeleter(dlcPath);
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);
            
            // list files
            foreach (var groupedFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                Assert.AreEqual(0, files.Count);
            }
        }

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) { }
    }
}