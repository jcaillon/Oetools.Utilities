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
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.Prolib;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Test.Archive.Prolib {
    
    /// <summary>
    /// Allows to pack files into a prolib file
    /// </summary>
    internal class OeProlibArchiver : ArchiverBase, IArchiver {
        
        private readonly Encoding _encoding;

        private readonly string _prolibPath;        
        
        internal OeProlibArchiver(string dlcPath, Encoding encoding) {
            _encoding = encoding;
            _prolibPath = Path.Combine(dlcPath, "bin", Utils.IsRuntimeWindowsPlatform ? "prolib.exe" : "prolib");
            if (!File.Exists(_prolibPath)) {
                throw new ArchiverException($"Could not find the prolib executable : {_prolibPath.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        /// <param name="archiveCompressionLevel"></param>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) { }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;

        /// <inheritdoc cref="IArchiverBasic.ArchiveFileSet"/>
        public int ArchiveFileSet(IEnumerable<IFileToArchive> filesToArchive) {
            var filesToPack = filesToArchive.ToList();
            filesToPack.ForEach(f => f.Processed = false);
            int totalFiles = filesToPack.Count;
            int totalFilesDone = 0;
            foreach (var plGroupedFiles in filesToPack.GroupBy(f => f.ArchivePath)) {
                string uniqueTempFolder = null;
                try {
                    var archiveFolder = CreateArchiveFolder(plGroupedFiles.Key);

                    // create a unique temp folder for this .pl
                    uniqueTempFolder = Path.Combine(archiveFolder, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}");
                    var dirInfo = Directory.CreateDirectory(uniqueTempFolder);
                    dirInfo.Attributes |= FileAttributes.Hidden;

                    var subFolders = new Dictionary<string, List<FilesToMove>>();
                    foreach (var file in plGroupedFiles) {
                        var subFolderPath = Path.GetDirectoryName(Path.Combine(uniqueTempFolder, file.PathInArchive));
                        if (!string.IsNullOrEmpty(subFolderPath)) {
                            if (!subFolders.ContainsKey(subFolderPath)) {
                                subFolders.Add(subFolderPath, new List<FilesToMove>());
                                if (!Directory.Exists(subFolderPath)) {
                                    Directory.CreateDirectory(subFolderPath);
                                }
                            }

                            if (File.Exists(file.SourcePath)) {
                                subFolders[subFolderPath].Add(new FilesToMove(file.SourcePath, Path.Combine(uniqueTempFolder, file.PathInArchive), file.PathInArchive));
                            }
                        }
                    }

                    var prolibExe = new ProcessIo(_prolibPath) {
                        WorkingDirectory = uniqueTempFolder
                    };

                    foreach (var subFolder in subFolders) {
                        _cancelToken?.ThrowIfCancellationRequested();

                        // move files to the temp subfolder
                        Parallel.ForEach(subFolder.Value, file => {
                            if (file.Move) {
                                File.Move(file.Origin, file.Temp);
                            } else {
                                File.Copy(file.Origin, file.Temp);
                            }
                        });

                        // for files containing a space, we don't have a choice, call extract for each...
                        foreach (var file in subFolder.Value.Where(f => f.RelativePath.Contains(" "))) {
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -create -nowarn -add {file.RelativePath.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to pack {file.Origin.PrettyQuote()} into {plGroupedFiles.Key.PrettyQuote()} and relative archive path {file.RelativePath}.", new ArchiverException(prolibExe.BatchOutput.ToString()));
                            }
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

                            File.WriteAllText(pfPath, pfContent.ToString(), _encoding);

                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to pack to {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
                            }

                            if (File.Exists(pfPath)) {
                                File.Delete(pfPath);
                            }
                        }

                        // move files from the temp subfolder
                        foreach (var file in subFolder.Value) {
                            try {
                                if (file.Move) {
                                    File.Move(file.Temp, file.Origin);
                                } else if (!File.Exists(file.Temp)) {
                                    throw new ArchiverException($"Failed to move back the temporary file {file.Origin} from {file.Temp}.");
                                }
                            } catch (Exception e) {
                                throw new ArchiverException($"Failed to move back the temporary file {file.Origin} from {file.Temp}.", e);
                            }

                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(plGroupedFiles.Key, file.RelativePath, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        }

                    }

                    // compress .pl
                    prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -compress -nowarn");

                    foreach (var file in plGroupedFiles) {
                        file.Processed = true;
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to pack to {plGroupedFiles.Key.PrettyQuote()}.", e);
                } finally {
                    // delete temp folder
                    if (Directory.Exists(uniqueTempFolder)) {
                        Directory.Delete(uniqueTempFolder, true);
                    }
                }
            }

            return totalFilesDone;
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            if (!File.Exists(archivePath)) {
                return Enumerable.Empty<IFileInArchive>();
            }
            
            var prolibExe = new ProcessIo(_prolibPath);

            if (!prolibExe.TryExecute($"{archivePath.CliQuoter()} -list")) { // -date mdy
                throw new Exception("Error while listing files from a .pl.", new Exception(prolibExe.BatchOutput.ToString()));
            }

            var outputList = new List<IFileInArchive>();
            var regex = new Regex(@"^(.+)\s+(\d+)\s+(\w+)\s+(\d+)\s+(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})\s(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})");
            foreach (var output in prolibExe.StandardOutputArray) {
                var match = regex.Match(output);
                if (match.Success) {
                    // Third match is the file type. PROLIB recognizes two file types: R (r-code file type) and O (any other file type).
                    // Fourth is the offset, the distance, in bytes, of the start of the file from the beginning of the library.
                    var type = match.Groups[3].Value;
                    var newFile = new FileInProlib {
                        PathInArchive = match.Groups[1].Value.TrimEnd(),
                        SizeInBytes = ulong.Parse(match.Groups[2].Value),
                        IsRcode = !string.IsNullOrEmpty(type) && type[0] == 'R'
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
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDeleteIn) {            
            var filesToDelete = filesToDeleteIn.ToList();
            filesToDelete.ForEach(f => f.Processed = false);
            int totalFiles = filesToDelete.Count;
            int totalFilesDone = 0;
            var prolibExe = new ProcessIo(_prolibPath);
            foreach (var plGroupedFiles in filesToDelete.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(plGroupedFiles.Key)) {
                    continue;
                }
                try {
                    var archiveFolder = Path.GetDirectoryName(plGroupedFiles.Key);
                    if (!string.IsNullOrEmpty(archiveFolder)) {
                        prolibExe.WorkingDirectory = archiveFolder;
                    }

                    // process only files that actually exist
                    var archiveFileList = ListFiles(plGroupedFiles.Key).Select(f => f.PathInArchive).ToHashSet();
                    var plGroupedFilesFiltered = plGroupedFiles.Where(f => archiveFileList.Contains(f.PathInArchive)).ToList();

                    // for files containing a space, we don't have a choice, call delete for each...
                    foreach (var file in plGroupedFilesFiltered.Where(deploy => deploy.PathInArchive.Contains(" "))) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -nowarn -delete {file.PathInArchive.CliQuoter()}")) {
                            throw new ArchiverException($"Failed to delete {file.PathInArchive.PrettyQuote()} in {plGroupedFiles.Key.PrettyQuote()}.", new ArchiverException(prolibExe.BatchOutput.ToString()));
                        }
                        totalFilesDone++;
                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(plGroupedFiles.Key, file.PathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                    }

                    _cancelToken?.ThrowIfCancellationRequested();
                    var remainingFiles = plGroupedFilesFiltered.Where(deploy => !deploy.PathInArchive.Contains(" ")).ToList();
                    if (remainingFiles.Count > 0) {
                        // for the other files, we can use the -pf parameter
                        var pfContent = new StringBuilder();
                        pfContent.AppendLine("-nowarn");
                        pfContent.AppendLine("-delete");
                        foreach (var file in remainingFiles) {
                            pfContent.AppendLine(file.PathInArchive);
                        }

                        var pfPath = $"{plGroupedFiles.Key}~{Path.GetRandomFileName()}.pf";

                        File.WriteAllText(pfPath, pfContent.ToString(), _encoding);
                    
                        // now we just need to add the content of temp folders into the .pl
                        if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                            throw new ArchiverException($"Failed to delete files in {plGroupedFiles.Key.PrettyQuote()}.", new ArchiverException(prolibExe.BatchOutput.ToString()));
                        }
                        foreach (var file in remainingFiles) {
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(plGroupedFiles.Key, file.PathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        }
                    
                        if (File.Exists(pfPath)) {
                            File.Delete(pfPath);
                        }
                    }
                    
                    foreach (var file in plGroupedFiles) {
                        file.Processed = true;
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to process {plGroupedFiles.Key.PrettyQuote()}.", e);
                }
            }
            
            return totalFilesDone;
        }
        
        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMoveIn) {
            var filesToMove = filesToMoveIn.ToList();
            filesToMove.ForEach(f => f.Processed = false);
            int totalFiles = filesToMove.Count;
            int totalFilesDone = 0;
            foreach (var plGroupedFiles in filesToMove.GroupBy(f => f.ArchivePath)) {
                string uniqueTempFolder = null;
                try {
                    // process only files that actually exist
                    var archiveFileList = ListFiles(plGroupedFiles.Key).Select(f => f.PathInArchive).ToHashSet();
                    var plGroupedFilesFiltered = plGroupedFiles.Where(f => archiveFileList.Contains(f.PathInArchive)).ToList();

                    if (!plGroupedFilesFiltered.Any()) {
                        continue;
                    }
                    
                    var archiveFolder = CreateArchiveFolder(plGroupedFiles.Key);

                    // create a unique temp folder for this .pl
                    uniqueTempFolder = Path.Combine(archiveFolder, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}");
                    var dirInfo = Directory.CreateDirectory(uniqueTempFolder);
                    dirInfo.Attributes |= FileAttributes.Hidden;

                    var subFolders = new Dictionary<string, List<FilesToMove>>();
                    foreach (var file in plGroupedFilesFiltered) {
                        var subFolderPath = Path.GetDirectoryName(Path.Combine(uniqueTempFolder, file.NewRelativePathInArchive));
                        if (!string.IsNullOrEmpty(subFolderPath)) {
                            if (!subFolders.ContainsKey(subFolderPath)) {
                                subFolders.Add(subFolderPath, new List<FilesToMove>());
                                if (!Directory.Exists(subFolderPath)) {
                                    Directory.CreateDirectory(subFolderPath);
                                }
                            }
                            subFolders[subFolderPath].Add(new FilesToMove(file.PathInArchive, Path.Combine(uniqueTempFolder, file.NewRelativePathInArchive), file.NewRelativePathInArchive));
                        }
                    }

                    var prolibExe = new ProcessIo(_prolibPath);

                    foreach (var subFolder in subFolders) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        
                        foreach (var file in subFolder.Value) {
                            prolibExe.WorkingDirectory = Path.GetDirectoryName(file.Temp);
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -nowarn -yank {file.Origin.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to extract {file.Origin.PrettyQuote()} from {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
                            }
                            
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -nowarn -delete {file.Origin.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to delete {file.Origin.PrettyQuote()} in {plGroupedFiles.Key.PrettyQuote()}.", new ArchiverException(prolibExe.BatchOutput.ToString()));
                            }

                            File.Move(Path.Combine(prolibExe.WorkingDirectory, Path.GetFileName(file.Origin)), file.Temp);

                            prolibExe.WorkingDirectory = uniqueTempFolder;
                            prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -nowarn -delete {file.RelativePath.CliQuoter()}");
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -create -nowarn -add {file.RelativePath.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to pack {file.Origin.PrettyQuote()} into {plGroupedFiles.Key.PrettyQuote()} and relative archive path {file.RelativePath}.", new ArchiverException(prolibExe.BatchOutput.ToString()));
                            }
                            
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(plGroupedFiles.Key, file.RelativePath, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        }
                    }

                    // compress .pl
                    prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -compress -nowarn");

                    // delete temp folder
                    Directory.Delete(uniqueTempFolder, true);
                    
                    foreach (var file in plGroupedFiles) {
                        file.Processed = true;
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to pack to {plGroupedFiles.Key.PrettyQuote()}.", e);
                } finally {
                    // delete temp folder
                    if (Directory.Exists(uniqueTempFolder)) {
                        Directory.Delete(uniqueTempFolder, true);
                    }
                }
            }

            return totalFilesDone;
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtractIn) {
            var filesToExtract = filesToExtractIn.ToList();
            filesToExtract.ForEach(f => f.Processed = false);
            int totalFiles = filesToExtract.Count;
            int totalFilesDone = 0;
            var prolibExe = new ProcessIo(_prolibPath);
            foreach (var plGroupedFiles in filesToExtract.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(plGroupedFiles.Key)) {
                    continue;
                }
                
                // process only files that actually exist
                var archiveFileList = ListFiles(plGroupedFiles.Key).Select(f => f.PathInArchive).ToHashSet();
                var plGroupedFilesFiltered = plGroupedFiles.Where(f => archiveFileList.Contains(f.PathInArchive)).ToList();
                
                try {
                    foreach (var extractDirGroupedFiles in plGroupedFilesFiltered.GroupBy(f => Path.GetDirectoryName(f.ExtractionPath))) {
                        prolibExe.WorkingDirectory = extractDirGroupedFiles.Key;
                        Directory.CreateDirectory(extractDirGroupedFiles.Key);
                        
                        // for files containing a space, we don't have a choice, call extract for each...
                        foreach (var file in extractDirGroupedFiles.Where(deploy => deploy.PathInArchive.Contains(" "))) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            if (File.Exists(file.ExtractionPath)) {
                                File.Delete(file.ExtractionPath);
                            }
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -nowarn -yank {file.PathInArchive.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to extract {file.PathInArchive.PrettyQuote()} from {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
                            }
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(plGroupedFiles.Key, file.PathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        }
    
                        _cancelToken?.ThrowIfCancellationRequested();
                        var remainingFiles = extractDirGroupedFiles.Where(deploy => !deploy.PathInArchive.Contains(" ")).ToList();
                        if (remainingFiles.Count > 0) {
                            // for the other files, we can use the -pf parameter
                            var pfContent = new StringBuilder();
                            pfContent.AppendLine("-nowarn");
                            pfContent.AppendLine("-yank");
                            foreach (var file in remainingFiles) {
                                pfContent.AppendLine(file.PathInArchive);
                                if (File.Exists(file.ExtractionPath)) {
                                    File.Delete(file.ExtractionPath);
                                }
                            }
    
                            var pfPath = Path.Combine(extractDirGroupedFiles.Key, $"{Path.GetFileName(plGroupedFiles.Key)}~{Path.GetRandomFileName()}.pf");
    
                            File.WriteAllText(pfPath, pfContent.ToString(), _encoding);
    
                            if (!prolibExe.TryExecute($"{plGroupedFiles.Key.CliQuoter()} -pf {pfPath.CliQuoter()}")) {
                                throw new ArchiverException($"Failed to extract from {plGroupedFiles.Key.PrettyQuote()}.", new Exception(prolibExe.BatchOutput.ToString()));
                            }
    
                            foreach (var file in remainingFiles) {
                                totalFilesDone++;
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(plGroupedFiles.Key, file.PathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                            }
    
                            if (File.Exists(pfPath)) {
                                File.Delete(pfPath);
                            }
                        }
                    }
                    
                    foreach (var file in plGroupedFiles) {
                        file.Processed = true;
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to process {plGroupedFiles.Key.PrettyQuote()}.", e);
                }
                
            }
            
            return totalFilesDone;
        }

        private class FilesToMove {
            public string Origin { get; }
            public string Temp { get; }
            public string RelativePath { get; }
            public bool Move { get; }
            public FilesToMove(string origin, string temp, string relativePath) {
                Origin = origin;
                Temp = temp;
                RelativePath = relativePath;
                Move = Utils.ArePathOnSameDrive(origin, temp);
            }
        }
    }
}