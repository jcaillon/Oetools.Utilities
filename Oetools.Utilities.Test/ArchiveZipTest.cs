using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Oetools.Utilities.Test {
    [TestClass]
    public class ArchiveZipTest {
        private string TestFolder => TestHelper.GetTestFolder(nameof(ArchiveZipTest));

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
        public void TestMethod1() { }
    }
}