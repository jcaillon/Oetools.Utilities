using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Cab;

namespace Oetools.Utilities.Test.Tests {
    
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
        public void CreateCab() {
            
            var cabPath = Path.Combine(TestFolder, "out.cab");
            IPackager packager = new CabPackager(cabPath);
            var listFiles = new List<IFileToDeployInPackage>();

            var fileName = "file1.txt";
            var filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath,
                PackPath = cabPath,
                RelativePathInPack = fileName
            });
            
            fileName = "file2.txt";
            filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(new FileToDeployInPackage {
                From = filePath, 
                PackPath = cabPath,
                RelativePathInPack = Path.Combine("subfolder1", fileName)
            });
            
            packager.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            string smd5;
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(cabPath)) {
                    smd5 = Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
            
            File.WriteAllText(Path.Combine(_testFolder, "md5sum.txt"), smd5);
            
            Assert.AreEqual("v/cIRcdAofMUNMNDFB+o6A==", smd5);
        }

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) {
            
        }
    }
}