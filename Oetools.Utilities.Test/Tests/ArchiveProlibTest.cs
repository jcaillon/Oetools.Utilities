using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Prolib;

namespace Oetools.Utilities.Test.Tests {
    
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
        public void CreatePl() {

            var dlcPath = Environment.GetEnvironmentVariable("dlc");
            if (string.IsNullOrEmpty(dlcPath)) {
                return;
            }

            var prolib = Path.Combine(dlcPath, "bin", "prolib.exe");
            if (!File.Exists(prolib)) {
                return;
            }
            
            ProlibArchiver archiver = new ProlibArchiver(prolib);
            List<IFileToArchive> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.pl");
            TestHelper.CreateSourceFiles(listFiles);           
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);
            
            // verify
            foreach (var groupedFiles in listFiles.GroupBy(f => f.PackPath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                foreach (var file in files) {
                    Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInPack.Equals(file.RelativePathInPack)));
                }
                Assert.AreEqual(groupedFiles.ToList().Count, files.Count);
            }
            
            /*
            
            
            var cabPath = Path.Combine(TestFolder, "out.pl");
            IArchiver archiver = new ProlibArchiver(prolib);
            var listFiles = new List<IFileToArchive>();

            var fileName = "file1.txt";
            var filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToArchive {
                From = filePath,
                PackPath = cabPath,
                RelativePathInPack = fileName
            });
            
            fileName = "file2 with spaces.txt";
            filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToArchive {
                From = filePath, 
                PackPath = cabPath,
                RelativePathInPack = Path.Combine("subfolder1", fileName)
            });
            
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            var listedFiles = archiver.ListFiles(cabPath);
            
            Assert.AreEqual(listFiles.Count, listedFiles.Count);

            foreach (var prolibFile in listedFiles) {
                Assert.IsTrue(listFiles.Exists(f => f.RelativePathInPack.Equals(prolibFile.RelativePathInPack)));
            }
            */
        }

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) {
            
        }
    }
}