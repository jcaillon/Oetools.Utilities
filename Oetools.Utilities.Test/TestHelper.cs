#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (TestHelper.cs) is part of Oetools.Utilities.Test.
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
using Oetools.Utilities.Archive;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Openedge;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Test.Archive;

namespace Oetools.Utilities.Test {
    public static class TestHelper {
        
        private static readonly string TestFolder = Path.Combine(AppContext.BaseDirectory, "Tests");

        public static bool GetDlcPath(out string dlcPath) {
            dlcPath = ProUtilities.GetDlcPathFromEnv();
            if (string.IsNullOrEmpty(dlcPath)) {
                return false;
            }
            if (!Directory.Exists(dlcPath)) {
                return false;
            }
            return true;
        }
        
        public static bool GetProlibPath(out string prolibPath) {
            bool ret = GetDlcPath(out string dlcPath);
            if (!ret) {
                prolibPath = null;
                return false;
            }
            prolibPath = Path.Combine(dlcPath, "bin", Utils.IsRuntimeWindowsPlatform ? "prolib.exe" : "prolib");
            return File.Exists(prolibPath);
        }
        
        public static string GetTestFolder(string testName) {
            var path = Path.Combine(TestFolder, testName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static void CreateSourceFiles(List<IFileToArchive> listFiles) {
            foreach (var file in listFiles) {
                File.WriteAllText(file.SourcePath, Path.GetFileName(file.SourcePath));
            }
        }

        public static void CreateDatabaseFromDf(string targetDatabasePath, string dfPath) {
            if (!GetDlcPath(out string dlcPath)) {
                return;
            }

            using (var dbAdministrator = new DatabaseAdministrator(dlcPath)) {
                dbAdministrator.ProstrctCreate(targetDatabasePath, dbAdministrator.GenerateStructureFileFromDf(targetDatabasePath, dfPath), DatabaseBlockSize.S4096);
                dbAdministrator.Procopy(targetDatabasePath, DatabaseBlockSize.S4096);
                dbAdministrator.LoadDf(targetDatabasePath, dfPath);
            }
        }

        public static List<IFileToArchive> GetPackageTestFilesList(string testFolder, string outPackName) {
            return new List<IFileToArchive> {
                new FileToArchive {
                    SourcePath = Path.Combine(testFolder, "file1.txt"),
                    ArchivePath = Path.Combine(testFolder, outPackName),
                    RelativePathInArchive = "file1.txt"
                },
                new FileToArchive {
                    SourcePath = Path.Combine(testFolder, "file2.txt"),
                    ArchivePath = Path.Combine(testFolder, outPackName),
                    RelativePathInArchive = Path.Combine("subfolder1", "file2.txt")
                },
                new FileToArchive {
                    SourcePath = Path.Combine(testFolder, "file3.txt"),
                    ArchivePath = Path.Combine(testFolder, outPackName),
                    RelativePathInArchive = Path.Combine("subfolder1", "bla", "file3.txt")
                }
            };
        }
    }
}