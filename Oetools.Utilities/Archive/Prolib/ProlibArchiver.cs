#region header
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
        
        private readonly string _prolibPath;

        public ProlibArchiver(string dlcPath) {
            _prolibPath = Path.Combine(dlcPath, "bin", Utils.IsRuntimeWindowsPlatform ? "prolib.exe" : "prolib");
            if (!File.Exists(_prolibPath)) {
                throw new ArchiveException($"Could not find the prolib executable : {_prolibPath.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        /// <param name="compressionLevel"></param>
        public void SetCompressionLevel(CompressionLvl compressionLevel) { }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiveProgressionEventArgs> OnProgress;

        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public virtual void PackFileSet(IEnumerable<IFileToArchive> files) {
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

                    var prolibExe = new ProcessIo(_prolibPath) {
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
                        foreach (var file in subFolder.Value.Where(f => f.RelativePath.Contains(" "))) {
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -create -nowarn -add {file.RelativePath.CliQuoter()}")) {
                                throw new ArchiveException($"Failed to pack {file.Origin.PrettyQuote()} into {plGroupedFiles.Key.PrettyQuote()} and relative archive path {file.RelativePath}.", new ArchiveException(prolibExe.BatchOutput.ToString()));
                            }
                            OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.Origin, file.RelativePath));
                        }

                        var remainingFiles = subFolder.Value.Where(f => !f.RelativePath.Contains(" ")).ToList();
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
                                throw new ArchiveException($"Failed to pack to {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
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
                                    throw new ArchiveException($"Failed to move back the temporary file {file.Origin} from {file.Temp}.");
                                }
                            } catch (Exception e) {
                                throw new ArchiveException($"Failed to move back the temporary file {file.Origin} from {file.Temp}.", e);
                            }
                            OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.Origin, file.RelativePath));
                        });
                    }

                    // compress .pl
                    prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -compress -nowarn");

                    // delete temp folder
                    Directory.Delete(uniqueTempFolder, true);
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to pack to {plGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, plGroupedFiles.Key, null, null));
            }
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public List<IFileArchived> ListFiles(string archivePath) {
            var prolibExe = new ProcessIo(_prolibPath);

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
        
        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public void DeleteFileSet(IEnumerable<IFileToExtract> files) {
            var prolibExe = new ProcessIo(_prolibPath);
            foreach (var plGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    var archiveFolder = Path.GetDirectoryName(plGroupedFiles.Key);
                    if (!string.IsNullOrEmpty(archiveFolder)) {
                        prolibExe.WorkingDirectory = archiveFolder;
                    }

                    // for files containing a space, we don't have a choice, call delete for each...
                    foreach (var file in plGroupedFiles.Where(deploy => deploy.RelativePathInArchive.Contains(" "))) {
                        if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -delete {file.RelativePathInArchive.CliQuoter()}")) {
                            throw new ArchiveException($"Failed to delete {file.RelativePathInArchive.PrettyQuote()} in {plGroupedFiles.Key.PrettyQuote()}.", new ArchiveException(prolibExe.BatchOutput.ToString()));
                        }
                        OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, null, file.RelativePathInArchive));
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
                            throw new ArchiveException($"Failed to delete files in {plGroupedFiles.Key.PrettyQuote()}.", new ArchiveException(prolibExe.BatchOutput.ToString()));
                        }
                        foreach (var file in remainingFiles) {
                            OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, null, file.RelativePathInArchive));
                        }
                    
                        if (File.Exists(pfPath)) {
                            File.Delete(pfPath);
                        }
                    }
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to process {plGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, plGroupedFiles.Key, null, null));
            }
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public void ExtractFileSet(IEnumerable<IFileToExtract> files) {
            var prolibExe = new ProcessIo(_prolibPath);
            foreach (var plGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    foreach (var extractDirGroupedFiles in plGroupedFiles.GroupBy(f => Path.GetDirectoryName(f.ExtractionPath))) {
                        prolibExe.WorkingDirectory = extractDirGroupedFiles.Key;
                        Directory.CreateDirectory(extractDirGroupedFiles.Key);
                        
                        // for files containing a space, we don't have a choice, call extract for each...
                        foreach (var file in extractDirGroupedFiles.Where(deploy => deploy.RelativePathInArchive.Contains(" "))) {
                            if (File.Exists(file.ExtractionPath)) {
                                File.Delete(file.ExtractionPath);
                            }
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -yank {file.RelativePathInArchive.CliQuoter()}")) {
                                throw new ArchiveException($"Failed to extract {file.RelativePathInArchive.PrettyQuote()} from {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
                            }
                            OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.ExtractionPath, file.RelativePathInArchive));
                        }
    
                        var remainingFiles = extractDirGroupedFiles.Where(deploy => !deploy.RelativePathInArchive.Contains(" ")).ToList();
                        if (remainingFiles.Count > 0) {
                            // for the other files, we can use the -pf parameter
                            var pfContent = new StringBuilder();
                            pfContent.AppendLine("-yank");
                            foreach (var file in remainingFiles) {
                                pfContent.AppendLine(file.RelativePathInArchive);
                                if (File.Exists(file.ExtractionPath)) {
                                    File.Delete(file.ExtractionPath);
                                }
                            }
    
                            var pfPath = Path.Combine(extractDirGroupedFiles.Key, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}.pf");
    
                            File.WriteAllText(pfPath, pfContent.ToString(), Encoding.Default);
    
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                                throw new ArchiveException($"Failed to extract from {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
                            }
    
                            foreach (var file in remainingFiles) {
                                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, plGroupedFiles.Key, file.ExtractionPath, file.RelativePathInArchive));
                            }
    
                            if (File.Exists(pfPath)) {
                                File.Delete(pfPath);
                            }
                        }
                    }
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to process {plGroupedFiles.Key.PrettyQuote()}.", e);
                }
                
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, plGroupedFiles.Key, null, null)); 
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