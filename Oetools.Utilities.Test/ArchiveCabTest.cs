using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Cab;
using Oetools.Utilities.Archive.Compression;

namespace Oetools.Utilities.Test {
    
    [TestClass]
    public class ArchiveCabTest {
        
        private string TestFolder => TestHelper.GetTestFolder(nameof(ArchiveCabTest));

        [TestInitialize]
        public void Init() {
            
        }
        
        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }

        [TestMethod]
        public void CreateCab() {
            var cabPath = Path.Combine(TestFolder, "out.cab");
            IPackager packager = new CabPackager(cabPath);
            var listFiles = new Dictionary<string, IFileToDeployInPackage>();

            var fileName = "file1.txt";
            var filePath = Path.Combine(TestFolder, fileName);
            File.WriteAllText(filePath, fileName);
            listFiles.Add(fileName, new FileToDeployInPackage {
                From = filePath, 
                PackPath = cabPath,
                RelativePathInPack = fileName
            });
            
            //fileName = "file2.txt";
            //filePath = Path.Combine(TestFolder, fileName);
            //File.WriteAllText(filePath, fileName);
            //listFiles.Add(fileName, new FileToDeployInPackage {
            //    From = filePath, 
            //    PackPath = cabPath,
            //    RelativePathInPack = Path.Combine("subfolder1", fileName)
            //});
            
            packager.PackFileSet(listFiles, CompressionLevel.None, ProgressHandler);
        }

        private void ProgressHandler(object sender, ArchiveProgressEventArgs e) {
            
        }
        
    }
}