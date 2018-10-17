#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (IArchiveTest.cs) is part of Oetools.Utilities.Test.
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Test.Archive {
    
    public class ArchiveTest {

        private int _nbFileProcessed;
        private int _nbArchiveFinished;
        
        protected void CreateArchive(IArchiver archiver, List<FileInArchive> listFiles) {
            archiver.OnProgress += ArchiverOnOnProgress;
            
            _nbFileProcessed = 0;
            _nbArchiveFinished = 0;

            var modifiedList = listFiles.GetRange(1, listFiles.Count - 1);
            
            // try to add a non existing file
            modifiedList.Add(new FileInArchive {
                ArchivePath = listFiles.First().ArchivePath,
                ExtractionPath = listFiles.First().ExtractionPath,
                RelativePathInArchive = "random.name"
            });
            Assert.AreEqual(modifiedList.Count - 1, archiver.PackFileSet(modifiedList));
            
            // test the update of archives
            modifiedList = listFiles.GetRange(0, 1);
            Assert.AreEqual(modifiedList.Count, archiver.PackFileSet(modifiedList));
 
            foreach (var archive in listFiles.GroupBy(f => f.ArchivePath)) {
                if (Directory.Exists(Path.GetDirectoryName(archive.Key))) {
                    Assert.IsTrue(File.Exists(archive.Key), $"The archive does not exist : {archive}");
                }
            }
            
            archiver.OnProgress -= ArchiverOnOnProgress;
            Assert.AreEqual(listFiles.Count, _nbFileProcessed, "Problem in the progress event");
            Assert.AreEqual(listFiles.GroupBy(f => f.ArchivePath).Count() + 1, _nbArchiveFinished, "Problem in the progress event, number of archives");
        }

        protected void ListArchive(IArchiver archiver, List<FileInArchive> listFiles) {
            foreach (var groupedTheoreticalFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var actualFiles = archiver.ListFiles(groupedTheoreticalFiles.Key).ToList();
                foreach (var theoreticalFile in groupedTheoreticalFiles) {
                    Assert.IsTrue(actualFiles.ToList().Exists(f => f.RelativePathInArchive.Replace("/", "\\").Equals(theoreticalFile.RelativePathInArchive)), $"Can't find file in list : {theoreticalFile.RelativePathInArchive}");
                }
                Assert.AreEqual(groupedTheoreticalFiles.Count(), actualFiles.Count, $"Wrong number of files listed : {groupedTheoreticalFiles.Count()}!={actualFiles.Count}");
            }
        }

        protected void Extract(IArchiver archiver, List<FileInArchive> listFiles) {
            archiver.OnProgress += ArchiverOnOnProgress;
            _nbFileProcessed = 0;
            _nbArchiveFinished = 0;
            
            // try to add a non existing file
            var modifiedList = listFiles.ToList();
            modifiedList.Add(new FileInArchive {
                ArchivePath = listFiles.First().ArchivePath,
                ExtractionPath = listFiles.First().ExtractionPath,
                RelativePathInArchive = "random.name"
            });
            Assert.AreEqual(modifiedList.Count - 1, archiver.ExtractFileSet(modifiedList));
            
            foreach (var fileToExtract in listFiles) {
                Assert.IsTrue(File.Exists(fileToExtract.ExtractionPath), $"Extracted file does not exist : {fileToExtract.ExtractionPath}");
                Assert.AreEqual(File.ReadAllText(fileToExtract.SourcePath), File.ReadAllText(fileToExtract.ExtractionPath), "Incoherent extracted file content");
            }
            
            archiver.OnProgress -= ArchiverOnOnProgress;
            Assert.AreEqual(listFiles.Count, _nbFileProcessed, "Problem in the progress event");
            Assert.AreEqual(listFiles.GroupBy(f => f.ArchivePath).Count(), _nbArchiveFinished, "Problem in the progress event, number of archives");
        }
        
        protected void DeleteFilesInArchive(IArchiver archiver, List<FileInArchive> listFiles) {
            archiver.OnProgress += ArchiverOnOnProgress;
            _nbFileProcessed = 0;
            _nbArchiveFinished = 0;

            // try to add a non existing file
            var modifiedList = listFiles.ToList();
            modifiedList.Add(new FileInArchive {
                ArchivePath = listFiles.First().ArchivePath,
                ExtractionPath = listFiles.First().ExtractionPath,
                RelativePathInArchive = "random.name"
            });
            Assert.AreEqual(modifiedList.Count - 1, archiver.DeleteFileSet(modifiedList));
            
            foreach (var groupedFiles in listFiles.GroupBy(f => f.ArchivePath)) {
                var files = archiver.ListFiles(groupedFiles.Key);
                Assert.AreEqual(0, files.Count(), $"The archive is not empty : {groupedFiles.Key}");
            }
            
            archiver.OnProgress -= ArchiverOnOnProgress;
            Assert.AreEqual(listFiles.Count, _nbFileProcessed, "Problem in the progress event");
            Assert.AreEqual(listFiles.GroupBy(f => f.ArchivePath).Count(), _nbArchiveFinished, "Problem in the progress event, number of archives");
        }

        private void ArchiverOnOnProgress(object sender, ArchiverEventArgs e) {
            if (e.EventType == ArchiverEventType.FileProcessed) {
                _nbFileProcessed++;
            } else if (e.EventType == ArchiverEventType.ArchiveCompleted) {
                _nbArchiveFinished++;
            }
        }
        
        protected List<FileInArchive> GetPackageTestFilesList(string testFolder, string archivePath) {
            var outputList = new List<FileInArchive> {
                new FileInArchive {
                    SourcePath = Path.Combine(testFolder, "file 0.txt"),
                    ArchivePath = archivePath,
                    RelativePathInArchive = "file 0.txt",
                    ExtractionPath = Path.Combine(testFolder, "extract", Path.GetFileName(archivePath) ?? "", "file 0.txt")
                },
                new FileInArchive {
                    SourcePath = Path.Combine(testFolder, "file1.txt"),
                    ArchivePath = archivePath,
                    RelativePathInArchive = "file1.txt",
                    ExtractionPath = Path.Combine(testFolder, "extract", Path.GetFileName(archivePath) ?? "", "file1.txt")
                },
                new FileInArchive {
                    SourcePath = Path.Combine(testFolder, "file2.txt"),
                    ArchivePath = archivePath,
                    RelativePathInArchive = Path.Combine("subfolder1", "file2.txt"),
                    ExtractionPath = Path.Combine(testFolder, "extract", Path.GetFileName(archivePath) ?? "", "subfolder1", "file2.txt")
                },
                new FileInArchive {
                    SourcePath = Path.Combine(testFolder, "file3.txt"),
                    ArchivePath = archivePath,
                    RelativePathInArchive = Path.Combine("subfolder1", "bla bla", "file3.txt"),
                    ExtractionPath = Path.Combine(testFolder, "extract", Path.GetFileName(archivePath) ?? "", "subfolder1", "bla bla", "file3.txt")
                }
            };
            foreach (var file in outputList) {
                File.WriteAllText(file.SourcePath, $"\"{Path.GetFileName(file.SourcePath)}\"");
                if (File.Exists(file.ExtractionPath)) {
                    File.Delete(file.ExtractionPath);
                }
            }
            foreach (var cabGrouped in outputList.GroupBy(f => f.ArchivePath)) {
                if (File.Exists(cabGrouped.Key)) {
                    File.Delete(cabGrouped.Key);
                }
            }
            return outputList;
        }
        
    }
}