using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            
            CabPackager packager = new CabPackager();
            List<IFileToDeployInPackage> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.cab");
            TestHelper.CreateSourceFiles(listFiles);           
            packager.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);
            
            // verify
            foreach (var groupedFiles in listFiles.GroupBy(f => f.PackPath)) {
                var files = packager.ListFiles(groupedFiles.Key);
                foreach (var file in files) {
                    Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInPack.Equals(file.RelativePathInPack)));
                }
                Assert.AreEqual(groupedFiles.ToList().Count, files.Count);
            }
            
        }

        private void ProgressHandler(object sender, ArchiveProgressionEventArgs e) {
            
        }
    }
}