﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProlibArchiver.cs) is part of Oetools.Utilities.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Archive.Prolib {
    /// <summary>
    ///     Allows to pack files into a prolib file
    /// </summary>
    public class ProlibArchiver : Archiver, IArchiver {
        
        protected readonly string DlcPath;
        
        /// <summary>
        /// Returns the path to _dbutil (or null if not found in the dlc folder)
        /// </summary>
        protected string ProliPath {
            get {
                string exeName = Utils.IsRuntimeWindowsPlatform ? "prolib.exe" : "prolib";
                var outputPath = Path.Combine(DlcPath, "bin", exeName);
                return File.Exists(outputPath) ? outputPath : null;
            }
        }

        public ProlibArchiver(string dlcPath) {
            DlcPath = dlcPath;
        }

        public virtual void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            foreach (var plGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    var archiveFolder = CreateArchiveFolder(plGroupedFiles.Key);

                    // create a unique temp folder for this .pl
                    var uniqueTempFolder = Path.Combine(archiveFolder, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}");
                    var dirInfo = Directory.CreateDirectory(uniqueTempFolder);
                    dirInfo.Attributes |= FileAttributes.Hidden;

                    //plGroupedFiles.GroupBy(f => Path.GetDirectoryName(Path.Combine(uniqueTempFolder, f.RelativePathInPack)))

                    var subFolders = new Dictionary<string, List<FilesToMove>>();

                    foreach (var file in plGroupedFiles) {
                        var subFolderPath = Path.GetDirectoryName(Path.Combine(uniqueTempFolder, file.RelativePathInArchive));
                        if (!string.IsNullOrEmpty(subFolderPath)) {
                            if (!subFolders.ContainsKey(subFolderPath)) {
                                subFolders.Add(subFolderPath, new List<FilesToMove>());
                                if (!Directory.Exists(subFolderPath)) {
                                    Directory.CreateDirectory(subFolderPath);
                                }
                            }

                            subFolders[subFolderPath].Add(new FilesToMove(file.SourcePath, Path.Combine(uniqueTempFolder, file.RelativePathInArchive), file.RelativePathInArchive));
                        }
                    }

                    var prolibExe = new ProcessIo(ProliPath) {
                        WorkingDirectory = uniqueTempFolder
                    };

                    foreach (var subFolder in subFolders) {
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
                        
                        // for files containing a space, we don't have a choice, call extract for each...
                        foreach (var file in subFolder.Value.Where(f => f.RelativePath.ContainsFast(" "))) {
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -create -nowarn -add {file.RelativePath.CliQuoter()}")) {
                                progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.RelativePath, new Exception(prolibExe.BatchOutput.ToString())));
                            }
                        }

                        var remainingFiles = subFolder.Value.Where(f => !f.RelativePath.ContainsFast(" ")).ToList();
                        if (remainingFiles.Count > 0) {
                            // for the other files, we can use the -pf parameter
                            var pfContent = new StringBuilder();
                            pfContent.AppendLine("-create -nowarn -add");
                            foreach (var file in remainingFiles) {
                                pfContent.AppendLine(file.RelativePath);
                            }

                            var pfPath = Path.Combine(uniqueTempFolder, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}.pf");

                            File.WriteAllText(pfPath, pfContent.ToString(), Encoding.Default);

                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                                foreach (var file in remainingFiles) {
                                    progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.RelativePath, new Exception(prolibExe.BatchOutput.ToString())));
                                }
                            }

                            if (File.Exists(pfPath)) {
                                File.Delete(pfPath);
                            }
                        }
                        
                        // move files from the temp subfolder
                        Parallel.ForEach(subFolder.Value, file => {
                            try {
                                if (file.Move) {
                                    File.Move(file.Temp, file.Origin);
                                } else if (!File.Exists(file.Temp)) {
                                    throw new Exception($"Couldn\'t move back the temporary file {file.Origin} from {file.Temp}");
                                }
                            } catch (Exception e) {
                                progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.RelativePath, e));
                            }
                        });
                    }

                    // compress .pl
                    prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -compress -nowarn");

                    // delete temp folder
                    Directory.Delete(uniqueTempFolder, true);
                } catch (Exception e) {
                    progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, plGroupedFiles.Key, null, e));
                }
            }
        }

        public List<IFileArchived> ListFiles(string archivePath) {
            var prolibExe = new ProcessIo(ProliPath);

            if (!prolibExe.TryExecute($"{archivePath.CliQuoter()} -list")) {
                throw new Exception("Error while listing files from a .pl", new Exception(prolibExe.BatchOutput.ToString()));
            }

            var outputList = new List<IFileArchived>();
            var regex = new Regex(@"^(.+)\s+(\d+)\s+(\w+)\s+(\d+)\s+(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})\s(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})");
            foreach (var output in prolibExe.StandardOutputArray) {
                var match = regex.Match(output);
                if (match.Success) {
                    var newFile = new ProlibFileArchived {
                        RelativePathInArchive = match.Groups[1].Value.TrimEnd(),
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
            }

            return outputList;
        }

        /// <summary>
        /// Extract the files given
        /// </summary>
        /// <param name="files"></param>
        /// <param name="extractionFolder"></param>
        public void ExtractFiles(List<IFileArchived> files, string extractionFolder) {
            var prolibExe = new ProcessIo(ProliPath);
            foreach (var plGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                prolibExe.WorkingDirectory = extractionFolder;

                // create the subfolders needed to extract each file
                foreach (var folder in files.Select(f => Path.GetDirectoryName(f.RelativePathInArchive)).Distinct(StringComparer.CurrentCultureIgnoreCase)) {
                    Directory.CreateDirectory(Path.Combine(extractionFolder, folder));
                }

                // for files containing a space, we don't have a choice, call extract for each...
                foreach (var file in files.Where(deploy => deploy.RelativePathInArchive.ContainsFast(" "))) {
                    if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -extract {file.RelativePathInArchive.CliQuoter()}")) {
                        throw new Exception("Error while extracting a file from a .pl", new Exception(prolibExe.BatchOutput.ToString()));
                    }
                }

                var remainingFiles = files.Where(deploy => !deploy.RelativePathInArchive.ContainsFast(" ")).ToList();
                if (remainingFiles.Count > 0) {
                    // for the other files, we can use the -pf parameter
                    var pfContent = new StringBuilder();
                    pfContent.AppendLine("-extract");
                    foreach (var file in remainingFiles) {
                        pfContent.AppendLine(file.RelativePathInArchive);
                    }

                    var pfPath = Path.Combine(extractionFolder, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}.pf");

                    File.WriteAllText(pfPath, pfContent.ToString(), Encoding.Default);

                    if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                        throw new Exception("Error while extracting a file from a .pl", new Exception(prolibExe.BatchOutput.ToString()));
                    }

                    if (File.Exists(pfPath)) {
                        File.Delete(pfPath);
                    }
                }
            }
        }

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
    }
}