#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ArchiveProlibTest.cs) is part of Oetools.Utilities.Test.
// 
// Oetools.Utilities.Test is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities.Test is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities.Test. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Prolib;
using Oetools.Utilities.Archive.Prolib.Core;

namespace Oetools.Utilities.Test.Archive.Prolib {
    
    [TestClass]
    public class ArchiveProlibTest : ArchiveTest {
        
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
        public void Test() {
            IArchiver archiver = Archiver.New(ArchiverType.Prolib);

            var listFiles = GetPackageTestFilesList(TestFolder, Path.Combine(TestFolder, "archives", "test1.pl"));
            listFiles.AddRange(GetPackageTestFilesList(TestFolder, Path.Combine(TestFolder, "archives", "test2.pl")));
            
            WholeTest(archiver, listFiles);
        }
        
        [TestMethod]
        public void TestCompareWithOeProlib() {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                return;
            }
            
            OeProlibArchiver oeArchiver;
            
            try {
                oeArchiver = new OeProlibArchiver(dlcPath, Encoding.Default);
            } catch (ArchiverException e) {
                Console.WriteLine($"Cancelling test, prolib not found! : {e.Message}");
                return;
            }
            
            IArchiver archiver = Archiver.New(ArchiverType.Prolib);

            //var list = archiver.ListFiles(@"C:\Users\Julien\Desktop\pl\v11_2files.pl");

            var prolib = new ProLibrary(@"C:\Users\Julien\Desktop\pl\v11_2files.pl", null);
            prolib = new ProLibrary(@"C:\Users\Julien\Desktop\pl\v7_2files.pl", null);
            var files = prolib.Files;
            prolib = new ProLibrary(@"C:\Users\Julien\Desktop\pl\OpenEdge.Core.pl", null);
            files = prolib.Files;

        }
        
    }
}