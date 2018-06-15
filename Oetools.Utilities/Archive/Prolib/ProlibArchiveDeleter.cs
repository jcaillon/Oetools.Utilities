#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProlibDelete.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Archive.Prolib {
    /// <summary>
    ///     Allows to delete files in a prolib file
    /// </summary>
    public class ProlibArchiveDeleter : ProlibArchiver, IArchiver {
        public ProlibArchiveDeleter(string prolibExePath) : base(prolibExePath) { }

        public override void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            using (var prolibExe = new ProcessIo(ProlibExePath)) {
                foreach (var plGroupedFiles in files.GroupBy(f => f.PackPath)) {
                    
                    var archiveFolder = Path.GetDirectoryName(plGroupedFiles.Key);
                    if (!string.IsNullOrEmpty(archiveFolder)) {
                        prolibExe.WorkingDirectory = archiveFolder;
                    }

                    // for files containing a space, we don't have a choice, call delete for each...
                    foreach (var file in files.Where(deploy => deploy.RelativePathInPack.ContainsFast(" "))) {
                        prolibExe.Arguments = plGroupedFiles.Key.Quoter() + " -delete " + file.RelativePathInPack.Quoter();
                        var isOk = prolibExe.TryDoWait(true);
                        if (progressHandler != null)
                            progressHandler(this, new ArchiveProgressionEventArgs(plGroupedFiles.Key, file.RelativePathInPack, isOk ? null : new Exception(prolibExe.ErrorOutput.ToString())));
                    }

                    var remainingFiles = files.Where(deploy => !deploy.RelativePathInPack.ContainsFast(" ")).ToList();
                    if (remainingFiles.Count > 0) {
                        // for the other files, we can use the -pf parameter
                        var pfContent = new StringBuilder();
                        pfContent.AppendLine("-delete");
                        foreach (var file in remainingFiles) {
                            pfContent.AppendLine(file.RelativePathInPack);
                        }

                        Exception ex = null;
                        var pfPath = plGroupedFiles.Key + "~" + Path.GetRandomFileName() + ".pf";

                        try {
                            File.WriteAllText(pfPath, pfContent.ToString(), Encoding.Default);
                        } catch (Exception e) {
                            ex = e;
                        }

                        prolibExe.Arguments = plGroupedFiles.Key.Quoter() + " -pf " + pfPath.Quoter();
                        var isOk = prolibExe.TryDoWait(true);

                        try {
                            if (ex == null) {
                                File.Delete(pfPath);
                            }
                        } catch (Exception e) {
                            ex = e;
                        }

                        if (progressHandler != null) {
                            foreach (var file in files.Where(deploy => !deploy.RelativePathInPack.ContainsFast(" "))) {
                                progressHandler(this, new ArchiveProgressionEventArgs(plGroupedFiles.Key, file.RelativePathInPack, ex ?? (isOk ? null : new Exception(prolibExe.ErrorOutput.ToString()))));
                            }
                        }
                    }
                }
            }
        }
    }
}