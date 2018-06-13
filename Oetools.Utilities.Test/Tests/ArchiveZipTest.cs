using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Zip;

namespace Oetools.Utilities.Test.Tests {
    
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
        public void CreateZip() {
            var zipPath = Path.Combine(TestFolder, "out.zip");
            IPackager packager = new ZipPackager(zipPath);
            var listFiles = new List<IFileToDeployInPackage>();

            var fileName = "file1.txt";
            var filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath,
                PackPath = zipPath,
                RelativePathInPack = fileName
            });

            fileName = "file2.txt";
            filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = Path.Combine(TestFolder, fileName),
                PackPath = zipPath,
                RelativePathInPack = Path.Combine("subfolder1", fileName)
            });

            packager.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            var zipList = new List<string>();
            using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                foreach (ZipArchiveEntry entry in archive.Entries) {
                    zipList.Add(entry.FullName);
                }
            }

            foreach (var zippedFile in zipList) {
                Assert.IsTrue(listFiles.Exists(f => f.RelativePathInPack.Equals(zippedFile)));
            }
            Assert.AreEqual(listFiles.Count, zipList.Count);
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