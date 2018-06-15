using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using csdeployer.Lib.Compression;
using csdeployer.Lib.Compression.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Zip;
using CompressionLevel = System.IO.Compression.CompressionLevel;

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
            
            // creates the .zip
            IArchiver archiver = new ZipArchiver();
            List<IFileToArchive> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.zip");
            TestHelper.CreateSourceFiles(listFiles);           
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            // verify
            foreach (var groupedFiles in listFiles.GroupBy(f => f.PackPath)) {
                using (ZipArchive archive = ZipFile.OpenRead(groupedFiles.Key)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInPack.Equals(entry.FullName)));
                    }
                    Assert.AreEqual(groupedFiles.ToList().Count, archive.Entries.Count);
                }
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