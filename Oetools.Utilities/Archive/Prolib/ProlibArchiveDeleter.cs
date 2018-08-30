#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProlibArchiveDeleter.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Archive.Prolib {
    
    /// <summary>
    ///     Allows to delete files in a prolib file
    /// </summary>
    public class ProlibArchiveDeleter : ProlibArchiver {
        
        public ProlibArchiveDeleter(string dlcPath) : base(dlcPath) { }

        public override void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            var prolibExe = new ProcessIo(ProliPath);
            foreach (var plGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    var archiveFolder = Path.GetDirectoryName(plGroupedFiles.Key);
                    if (!string.IsNullOrEmpty(archiveFolder)) {
                        prolibExe.WorkingDirectory = archiveFolder;
                    }

                    // for files containing a space, we don't have a choice, call delete for each...
                    foreach (var file in plGroupedFiles.Where(deploy => deploy.RelativePathInArchive.Contains(" "))) {
                        if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -delete {file.RelativePathInArchive.CliQuoter()}")) {
                            progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.RelativePathInArchive, new Exception(prolibExe.BatchOutput.ToString())));
                        }
                    }

                    var remainingFiles = plGroupedFiles.Where(deploy => !deploy.RelativePathInArchive.Contains(" ")).ToList();
                    if (remainingFiles.Count > 0) {
                        // for the other files, we can use the -pf parameter
                        var pfContent = new StringBuilder();
                        pfContent.AppendLine("-delete");
                        foreach (var file in remainingFiles) {
                            pfContent.AppendLine(file.RelativePathInArchive);
                        }

                        var pfPath = $"{plGroupedFiles.Key}~{Path.GetRandomFileName()}.pf";

                        File.WriteAllText(pfPath, pfContent.ToString(), Encoding.Default);
                    
                        // now we just need to add the content of temp folders into the .pl
                        if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                            foreach (var file in remainingFiles) {
                                progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.RelativePathInArchive, new Exception(prolibExe.BatchOutput.ToString())));
                            }
                        }
                    
                        if (File.Exists(pfPath)) {
                            File.Delete(pfPath);
                        }
                    }
                } catch (Exception e) {
                    progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, plGroupedFiles.Key, null, e));
                }
            }
        }
    }
}