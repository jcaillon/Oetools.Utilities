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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotUtilities.Archive;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Prolib.Core;
using Oetools.Utilities.Openedge;

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
            IArchiverFullFeatured archiver = UoeArchiver.NewProlibArchiver();

            var listFiles = GetPackageTestFilesList(TestFolder, Path.Combine(TestFolder, "archives", "test1.pl"));
            listFiles.AddRange(GetPackageTestFilesList(TestFolder, Path.Combine(TestFolder, "archives", "test2.pl")));

            WholeTest(archiver, listFiles);
        }

        [TestMethod]
        public void ProlibraryTestEdgeCases() {
            if (!TestHelper.GetDlcPath(out string dlcPath) || UoeUtilities.GetProVersionFromDlc(dlcPath).Major != 11) {
                return;
            }

            // test the edge cases were the file entries fill 511/512/513 of the file entries block size
            var filePath = Path.Combine(TestFolder, "test_edge_cases");
            File.WriteAllText($"{filePath}1", "");
            File.WriteAllText($"{filePath}2", "");
            File.WriteAllText($"{filePath}3", "");
            File.WriteAllText($"{filePath}4", "");
            File.WriteAllText($"{filePath}5", "");

            for (int i = 65; i >= 63; i--) {
                // progress prolib
                OeProlibArchiver oeArchiver;
                try {
                    oeArchiver = new OeProlibArchiver(dlcPath, Encoding.Default);
                } catch (ArchiverException e) {
                    Console.WriteLine($"Cancelling test, prolib not found! : {e.Message}");
                    return;
                }
                oeArchiver.ArchiveFileSet(new List<IFileToArchive> {
                    new FileInArchive { SourcePath = $"{filePath}1", PathInArchive = new string('a', 83), ArchivePath = Path.Combine(TestFolder, "test_edge_cases_official.pl") },
                    new FileInArchive { SourcePath = $"{filePath}2", PathInArchive = new string('b', 84), ArchivePath = Path.Combine(TestFolder, "test_edge_cases_official.pl") },
                    new FileInArchive { SourcePath = $"{filePath}3", PathInArchive = new string('f', 84), ArchivePath = Path.Combine(TestFolder, "test_edge_cases_official.pl") },
                    new FileInArchive { SourcePath = $"{filePath}4", PathInArchive = new string('c', i), ArchivePath = Path.Combine(TestFolder, "test_edge_cases_official.pl") },
                    new FileInArchive { SourcePath = $"{filePath}5", PathInArchive = new string('d', 1), ArchivePath = Path.Combine(TestFolder, "test_edge_cases_official.pl") }
                });

                // our prolib
                File.Copy(Path.Combine(TestFolder, "test_edge_cases_official.pl"), Path.Combine(TestFolder, "test_edge_cases.pl"));
                using (var prolib = new ProLibrary(Path.Combine(TestFolder, "test_edge_cases.pl"), null)) {
                    prolib.Save();
                }

                Assert.IsTrue(File.ReadAllBytes(Path.Combine(TestFolder, "test_edge_cases_official.pl")).SequenceEqual(File.ReadAllBytes(Path.Combine(TestFolder, "test_edge_cases.pl"))), "file not recreated the same way : test_edge_cases.");

                File.Delete(Path.Combine(TestFolder, "test_edge_cases.pl"));
                File.Delete(Path.Combine(TestFolder, "test_edge_cases_official.pl"));
            }



        }

        [TestMethod]
        public void ReadAllKindsOfLib() {
            CreateProlibResources();
            var list = GetProlibResources();

            foreach (var prolibResource in list) {
                using (var prolib = new ProLibrary(Path.Combine(TestFolder, prolibResource.FileName), null)) {
                    Assert.AreEqual(prolibResource.ContainedFiles, string.Join(",", prolib.Files.Select(f => f.RelativePath)), $"Wrong file listing for {prolibResource.FileName}.");
                    foreach (var libraryFileEntry in prolib.Files) {
                        if (!File.Exists(Path.Combine(TestFolder, libraryFileEntry.RelativePath))) {
                            prolib.ExtractToFile(libraryFileEntry.RelativePath, Path.Combine(TestFolder, libraryFileEntry.RelativePath));

                        }
                    }
                }
            }

            Assert.IsTrue(File.ReadAllBytes(Path.Combine(TestFolder, "file")).SequenceEqual(new byte[] { 0xAA }), "file incorrect.");
            Assert.IsTrue(File.ReadAllBytes(Path.Combine(TestFolder, "file2")).SequenceEqual(new byte[] { 0xAA, 0xAA }), "file2 incorrect.");
            Assert.IsTrue(File.ReadAllBytes(Path.Combine(TestFolder, "file.r")).SequenceEqual(GetBytesFromResource("file.r")), "file.r incorrect.");

            foreach (var prolibResource in list.Where(f => f.IsCompressed)) {
                File.Copy(Path.Combine(TestFolder, prolibResource.FileName), Path.Combine(TestFolder, $"{prolibResource.FileName}_copy.pl"));
                using (var prolib = new ProLibrary(Path.Combine(TestFolder, $"{prolibResource.FileName}_copy.pl"), null)) {
                    prolib.Save();
                    var data = new byte[new FileInfo(Path.Combine(TestFolder, prolibResource.FileName)).Length];
                    File.ReadAllBytes(Path.Combine(TestFolder, $"{prolibResource.FileName}_copy.pl")).CopyTo(data, 0);

                    Assert.IsTrue(data.SequenceEqual(File.ReadAllBytes(Path.Combine(TestFolder, prolibResource.FileName))), $"file not recreated the same way : {prolibResource.FileName}.");
                }
            }

            if (!TestHelper.GetDlcPath(out string dlcPath) || UoeUtilities.GetProVersionFromDlc(dlcPath).Major != 11) {
                return;
            }
            OeProlibArchiver oeArchiver;
            try {
                oeArchiver = new OeProlibArchiver(dlcPath, Encoding.Default);
            } catch (ArchiverException e) {
                Console.WriteLine($"Cancelling test, prolib not found! : {e.Message}");
                return;
            }

            File.WriteAllBytes(Path.Combine(TestFolder, "file3"), new byte[]{ 0xAA, 0xAA, 0xAA });
            File.WriteAllBytes(Path.Combine(TestFolder, "file4"), new byte[]{ 0xAA, 0xAA, 0xAA, 0xAA });

            foreach (var prolibResource in list.Where(f => f.IsCompressed && f.Version == ProLibraryVersion.V11Standard)) {
                var path = Path.Combine(TestFolder, $"{prolibResource.FileName}_copy.pl");
                Assert.AreEqual(string.Join(",", prolibResource.ContainedFiles.Split(',').OrderBy(s => s)), string.Join(",", oeArchiver.ListFiles(path).Select(f => f.PathInArchive).OrderBy(s => s)), $"Wrong file listing 2 for {prolibResource.FileName}.");
                oeArchiver.ArchiveFileSet(new List<IFileToArchive> {
                    new FileInArchive {
                        SourcePath = Path.Combine(TestFolder, "file3"), PathInArchive = "sub/file3", ArchivePath = path
                    }
                });
                oeArchiver.ArchiveFileSet(new List<IFileToArchive> {
                    new FileInArchive {
                        SourcePath = Path.Combine(TestFolder, "file4"), PathInArchive = "sub/file4", ArchivePath = path
                    }
                });
                var fileCount = (string.IsNullOrEmpty(prolibResource.ContainedFiles) ? 0 : prolibResource.ContainedFiles.Split(',').Length) + 2;

                using (var prolib = new ProLibrary(path, null)) {
                    Assert.AreEqual(fileCount, prolib.Files.Count, $"Bad count for {prolibResource.FileName}.");
                    prolib.Save();
                }
                Assert.AreEqual(fileCount, oeArchiver.ListFiles(path).Count());
            }
        }

        private void CreateProlibResources() {
            foreach (var item in GetProlibResources()) {
                File.WriteAllBytes(Path.Combine(TestFolder, item.FileName), GetBytesFromResource(item.FileName));
            }
        }

        private byte[] GetBytesFromResource(string name) {
            return Resources.Resources.GetBytesFromResource($"Prolib.{name}");
        }

        private List<ProlibResource> GetProlibResources() {
            return new List<ProlibResource> {
                new ProlibResource("v11.pl", "file", ProLibraryVersion.V11Standard, true),
                new ProlibResource("v11add_delete.pl", "", ProLibraryVersion.V11Standard, false),
                new ProlibResource("v11add_delete_add.pl", "file", ProLibraryVersion.V11Standard, false),
                new ProlibResource("v11add_delete_compressed.pl", "", ProLibraryVersion.V11Standard, true),
                new ProlibResource("v11_2bytes.pl", "file2", ProLibraryVersion.V11Standard, true),
                new ProlibResource("v11_2files.pl", "file,file2", ProLibraryVersion.V11Standard, false),
                new ProlibResource("v11_2files_compress.pl", "file,file2", ProLibraryVersion.V11Standard, true),
                new ProlibResource("v11_rcode.pl", "file.r,file,file2", ProLibraryVersion.V11Standard, true),
                new ProlibResource("v12memshared.pl", "file", ProLibraryVersion.V12MemoryMapped, true),
                new ProlibResource("v7.pl", "file", ProLibraryVersion.V7Standard, true),
                new ProlibResource("v7add_delete.pl", "", ProLibraryVersion.V7Standard, false),
                new ProlibResource("v7add_delete_add.pl", "file", ProLibraryVersion.V7Standard, false),
                new ProlibResource("v7_2bytes.pl", "file2", ProLibraryVersion.V7Standard, true),
                new ProlibResource("v7_2files.pl", "file,file2", ProLibraryVersion.V7Standard, false),
                new ProlibResource("v7_2files_compress.pl", "file,file2", ProLibraryVersion.V7Standard, true),
                new ProlibResource("v7_rcode.pl", "file.r,file,file2", ProLibraryVersion.V7Standard, true),
                new ProlibResource("v8memshared.pl", "file", ProLibraryVersion.V8MemoryMapped, true)
            };
        }

        private class ProlibResource {
            public string FileName { get; }
            public string ContainedFiles { get; }
            public ProLibraryVersion Version { get; }
            public bool IsCompressed { get; }
            public ProlibResource(string fileName, string containedFiles, ProLibraryVersion version, bool isCompressed) {
                FileName = fileName;
                ContainedFiles = containedFiles;
                Version = version;
                IsCompressed = isCompressed;
            }
        }
    }
}
