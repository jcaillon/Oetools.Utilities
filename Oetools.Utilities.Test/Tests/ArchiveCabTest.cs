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