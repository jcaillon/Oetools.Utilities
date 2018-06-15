#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProlibPackager.cs) is part of csdeployer.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Archive.Prolib {
    /// <summary>
    ///     Allows to pack files into a prolib file
    /// </summary>
    public class ProlibArchiver : Archiver, IArchiver {
        
        #region Private

        protected readonly string ProlibExePath;

        #endregion

        #region Life and death

        public ProlibArchiver(string prolibExePath) {
            ProlibExePath = prolibExePath;
        }

        #endregion

        #region Methods

        public virtual void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            foreach (var plGroupedFiles in files.GroupBy(f => f.PackPath)) {
                var archiveFolder = CreateArchivePath(plGroupedFiles.Key);

                // create a unique temp folder for this .pl
                var uniqueTempFolder = Path.Combine(archiveFolder, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}");
                var dirInfo = Directory.CreateDirectory(uniqueTempFolder);
                dirInfo.Attributes |= FileAttributes.Hidden;

                //plGroupedFiles.GroupBy(f => Path.GetDirectoryName(Path.Combine(uniqueTempFolder, f.RelativePathInPack)))

                var subFolders = new Dictionary<string, List<FilesToMove>>();

                foreach (var file in files) {
                    var subFolderPath = Path.GetDirectoryName(Path.Combine(uniqueTempFolder, file.RelativePathInPack));
                    if (!string.IsNullOrEmpty(subFolderPath)) {
                        if (!subFolders.ContainsKey(subFolderPath)) {
                            subFolders.Add(subFolderPath, new List<FilesToMove>());
                            if (!Directory.Exists(subFolderPath)) {
                                Directory.CreateDirectory(subFolderPath);
                            }
                        }

                        subFolders[subFolderPath].Add(new FilesToMove(file.From, Path.Combine(uniqueTempFolder, file.RelativePathInPack), file.RelativePathInPack));
                    }
                }

                var prolibExe = new ProcessIo(ProlibExePath) {
                    StartInfo = {
                        WorkingDirectory = uniqueTempFolder,
                    }
                };

                foreach (var subFolder in subFolders) {
                    Exception libException = null;

                    prolibExe.Arguments = $"{plGroupedFiles.Key.Quoter()} -create -nowarn -add {Path.Combine(subFolder.Key.Replace(uniqueTempFolder, "").TrimStart('\\'), "*").Quoter()}";

                    // move files to the temp subfolder
                    Parallel.ForEach(subFolder.Value, file => {
                        try {
                            if (file.Move) {
                                File.Move(file.Origin, file.Temp);
                            } else {
                                File.Copy(file.Origin, file.Temp);
                            }
                        } catch (Exception) {
                            // ignore
                        }
                    });

                    // now we just need to add the content of temp folders into the .pl
                    var prolibOk = prolibExe.TryDoWait(true);

                    // move files from the temp subfolder
                    Parallel.ForEach(subFolder.Value, file => {
                        Exception ex = null;
                        try {
                            if (file.Move) {
                                File.Move(file.Temp, file.Origin);
                            } else if (!File.Exists(file.Temp)) {
                                throw new Exception($"Couldn\'t move back the temporary file {file.Origin} from {file.Temp}");
                            }
                        } catch (Exception e) {
                            ex = e;
                        }

                        try {
                            progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(plGroupedFiles.Key, file.RelativePath, ex ?? (prolibOk ? null : new Exception(prolibExe.ErrorOutput.ToString()))));
                        } catch (Exception) {
                            // ignored
                        }
                    });
                }

                // compress .pl
                prolibExe.Arguments = $"{plGroupedFiles.Key.Quoter()} -compress -nowarn";
                prolibExe.TryDoWait(true);
                prolibExe.Kill();
                prolibExe.Dispose();

                // delete temp folder
                Directory.Delete(uniqueTempFolder, true);
            }
        }

        public List<IFileArchived> ListFiles(string archivePath) {
            using (var prolibExe = new ProcessIo(ProlibExePath)) {
                prolibExe.Arguments = $"{archivePath.Quoter()} -list";
                if (!prolibExe.TryDoWait(true)) {
                    throw new Exception("Error while listing files from a .pl", new Exception(prolibExe.ErrorOutput.ToString()));
                }

                var outputList = new List<IFileArchived>();
                var matches = new Regex(@"^(.+)\s+(\d+)\s+(\w+)\s+(\d+)\s+(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})\s(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})", RegexOptions.Multiline).Matches(prolibExe.StandardOutput.ToString());
                foreach (Match match in matches) {
                    var newFile = new ProlibFileArchived {
                        RelativePathInPack = match.Groups[1].Value.TrimEnd(),
                        SizeInBytes = ulong.Parse(match.Groups[2].Value),
                        Type = match.Groups[3].Value
                    };
                    if (DateTime.TryParseExact(match.Groups[5].Value, @"MM/dd/yy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) {
                        newFile.DateAdded = date;
                    }

                    if (DateTime.TryParseExact(match.Groups[6].Value, @"MM/dd/yy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) {
                        newFile.LastWriteTime = date;
                    }

                    outputList.Add(newFile);
                }

                return outputList;
            }
        }

        /// <summary>
        /// Extract the files given
        /// </summary>
        /// <param name="files"></param>
        /// <param name="extractionFolder"></param>
        public void ExtractFiles(List<IFileArchived> files, string extractionFolder) {
            using (var prolibExe = new ProcessIo(ProlibExePath)) {
                foreach (var plGroupedFiles in files.GroupBy(f => f.PackPath)) {
                    prolibExe.StartInfo.WorkingDirectory = extractionFolder;

                    // create the subfolders needed to extract each file
                    foreach (var folder in files
                        .Select(f => Path.GetDirectoryName(f.RelativePathInPack))
                        .Distinct(StringComparer.CurrentCultureIgnoreCase)) {
                        Directory.CreateDirectory(Path.Combine(extractionFolder, folder));
                    }

                    // for files containing a space, we don't have a choice, call extract for each...
                    foreach (var file in files.Where(deploy => deploy.RelativePathInPack.ContainsFast(" "))) {
                        prolibExe.Arguments = $"{plGroupedFiles.Key.Quoter()} -extract {file.RelativePathInPack.Quoter()}";
                        if (!prolibExe.TryDoWait(true)) {
                            throw new Exception("Error while extracting a file from a .pl", new Exception(prolibExe.ErrorOutput.ToString()));
                        }
                    }

                    var remainingFiles = files.Where(deploy => !deploy.RelativePathInPack.ContainsFast(" ")).ToList();
                    if (remainingFiles.Count > 0) {
                        // for the other files, we can use the -pf parameter
                        var pfContent = new StringBuilder();
                        pfContent.AppendLine("-extract");
                        foreach (var file in remainingFiles) {
                            pfContent.AppendLine(file.RelativePathInPack);
                        }

                        var pfPath = $"{extractionFolder}{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}.pf";

                        File.WriteAllText(pfPath, pfContent.ToString(), Encoding.Default);

                        prolibExe.Arguments = $"{plGroupedFiles.Key.Quoter()} -pf {pfPath.Quoter()}";
                        if (!prolibExe.TryDoWait(true)) {
                            throw new Exception("Error while extracting a file from a .pl", new Exception(prolibExe.ErrorOutput.ToString()));
                        }

                        if (File.Exists(pfPath))
                            File.Delete(pfPath);
                    }
                }
            }
        }

        #endregion

        #region FilesToMove

        private class FilesToMove {
            public string Origin { get; private set; }
            public string Temp { get; private set; }
            public string RelativePath { get; private set; }
            public bool Move { get; private set; }

            public FilesToMove(string origin, string temp, string relativePath) {
                Origin = origin;
                Temp = temp;
                RelativePath = relativePath;
                Move = origin.Length > 2 && temp.Length > 2 && origin.Substring(0, 2).EqualsCi(temp.Substring(0, 2));
            }
        }

        #endregion
    }
}