#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ArchiveZipTest.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Archive.Zip;

namespace Oetools.Utilities.Test.Archive.Zip {
    
    [TestClass]
    public class ArchiveZipTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ArchiveZipTest)));

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
        public void CreateZip_noCompression() {
            // creates the .zip
            IArchiver archiver = new ZipArchiver();
            archiver.SetCompressionLevel(CompressionLvl.None);
            
            List<IFileToArchive> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.zip");
            listFiles.AddRange(TestHelper.GetPackageTestFilesList(TestFolder, "out2.zip"));
            
            TestHelper.CreateSourceFiles(listFiles);
            archiver.PackFileSet(listFiles);

            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out.zip")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out2.zip")));
            
            // verify
            ListZip(listFiles);
        }

        private void ListZip(List<IFileToArchive> listFiles) {
            IArchiver archiver = new ZipArchiver();
            foreach (var groupedFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                foreach (var file in files) {
                    Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInArchive.Equals(file.RelativePathInArchive)));
                }
                Assert.AreEqual(groupedFiles.ToList().Count, files.Count);
            }
        }

        /*
         // does not add, will replace the existing .zip
        [TestMethod]
        public void AddToZip() {
            var zipPath = Path.Combine(TestFolder, "out.zip");
            IPackager packager = new ZipPackager(zipPath);
            
            var listFiles = new List<IFileToDeployInPackage>();

            var fileName = "file3.txt";
            var filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath,
                PackPath = zipPath,
                RelativePathInPack = fileName
            });

            fileName = "file4.txt";
            filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath,
                PackPath = zipPath,
                RelativePathInPack = Path.Combine("subfolder2", fileName)
            });

            packager.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            string smd5;
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(zipPath)) {
                    smd5 = Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }

            File.WriteAllText(Path.Combine(_testFolder, "md5sum.txt"), smd5);

            Assert.AreEqual("p6AQVqZ6uVQcWTg8TEe4tg==", smd5);
        }
        */

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) { }
    }
}