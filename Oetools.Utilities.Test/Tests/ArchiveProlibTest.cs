using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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

            //var dlcPath = Environment.GetEnvironmentVariable("dlc");
            var dlcPath = @"C:\progress\client\v117x_dv\dlc";
            if (string.IsNullOrEmpty(dlcPath)) {
                return;
            }

            var prolib = Path.Combine(dlcPath, "bin", "prolib.exe");
            if (!File.Exists(prolib)) {
                return;
            }
            
            var cabPath = Path.Combine(TestFolder, "out.pl");
            IPackager packager = new ProlibPackager(cabPath, prolib);
            var listFiles = new List<IFileToDeployInPackage>();

            var fileName = "file1.txt";
            var filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath,
                PackPath = cabPath,
                RelativePathInPack = fileName
            });
            
            fileName = "file2 with spaces.txt";
            filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath, 
                PackPath = cabPath,
                RelativePathInPack = Path.Combine("subfolder1", fileName)
            });
            
            packager.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            var lister = new ProlibListing(cabPath, prolib);
            var listedFiles = lister.ListFiles();
            
            Assert.AreEqual(listFiles.Count, listedFiles.Count);

            foreach (var prolibFile in listedFiles) {
                Assert.IsTrue(listFiles.Exists(f => f.RelativePathInPack.Equals(prolibFile.RelativePathInPack)));
            }
            
            string smd5;
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(cabPath)) {
                    smd5 = Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
            
            File.WriteAllText(Path.Combine(_testFolder, "md5sum.txt"), smd5);
            
            Assert.AreEqual("5tBnBYbzpeZwsGpSXl1Mew==", smd5);
        }

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) {
            
        }
    }
}