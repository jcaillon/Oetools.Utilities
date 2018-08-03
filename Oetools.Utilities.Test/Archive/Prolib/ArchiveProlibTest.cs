using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Prolib;

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
        public void CreatePl() {
            if (!TestHelper.GetProlibPath(out string prolib)) {
                return;
            }

            ProlibArchiver archiver = new ProlibArchiver(prolib);
            List<IFileToArchive> listFiles = TestHelper.GetPackageTestFilesList(TestFolder, "out.pl");
            listFiles.AddRange(TestHelper.GetPackageTestFilesList(TestFolder, "out2.pl"));
            
            TestHelper.CreateSourceFiles(listFiles);
            
            archiver.PackFileSet(listFiles, CompressionLvl.None, ProgressHandler);

            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out.pl")));
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, "out2.pl")));

            // verify
            ListPl(prolib, listFiles);
            
            // delete files
            DeleteFilesInPl(prolib, listFiles);
        }


        private void ListPl(string prolib, List<IFileToArchive> listFiles) {
            IArchiver archiver = new ProlibArchiver(prolib);
            foreach (var groupedFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                foreach (var file in files) {
                    Assert.IsTrue(groupedFiles.ToList().Exists(f => f.RelativePathInArchive.Equals(file.RelativePathInArchive)));
                }

                Assert.AreEqual(groupedFiles.ToList().Count, files.Count);
            }
        }
        
        
        private void DeleteFilesInPl(string prolib, List<IFileToArchive> listFiles) {
            IArchiver archiver = new ProlibArchiveDeleter(prolib);
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