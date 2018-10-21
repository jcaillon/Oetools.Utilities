#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileSystemArchiver.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Archive.Filesystem {
    
    /// <summary>
    /// In that case, a folder on the file system represents an archive.
    /// </summary>
    public class FileSystemArchiver : ArchiverBase, IArchiver {

        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) {
            // not applicable
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public int PackFileSet(IEnumerable<IFileToArchive> filesToPack) {
            return DoForFiles(filesToPack.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = f.SourcePath,
                    Target = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath()
                }
            ).ToList(), ActionType.Copy);
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            if (!Directory.Exists(archivePath)) {
                return Enumerable.Empty<IFileInArchive>();
            }
            var archivePathNormalized = archivePath.ToCleanPath();
            return Utils.EnumerateAllFiles(archivePath, SearchOption.AllDirectories, null, false, _cancelToken)
                .Select(path => {
                    var fileInfo = new FileInfo(path);
                    return new FileInFilesystem {
                        RelativePathInArchive = path.FromAbsolutePathToRelativePath(archivePathNormalized),
                        ArchivePath = archivePath,
                        SizeInBytes = (ulong) fileInfo.Length,
                        LastWriteTime = fileInfo.LastWriteTime
                    };
                });
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtract) {
            return DoForFiles(filesToExtract.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath(),
                    Target = f.ExtractionPath
                }
            ).ToList(), ActionType.Copy);
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDelete) {
            return DoForFiles(filesToDelete.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath()
                }
            ).ToList(), ActionType.Delete);
        }

        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMove) {
            return DoForFiles(filesToMove.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath(),
                    Target = Path.Combine(f.ArchivePath, f.NewRelativePathInArchive).ToCleanPath(),
                }
            ).ToList(), ActionType.Move);
        }
        
        private int DoForFiles(List<FsFile> files, ActionType actionType) {
            var totalFiles = files.Count;
            var totalFilesDone = 0;

            foreach (var archiveGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    if (actionType != ActionType.Delete) {
                        // create all necessary target folders
                        foreach (var dirGroupedFiles in archiveGroupedFiles.GroupBy(f => Path.GetDirectoryName(f.Target))) {
                            if (!Directory.Exists(dirGroupedFiles.Key)) {
                                Directory.CreateDirectory(dirGroupedFiles.Key);
                            }
                        }
                    }

                    foreach (var file in archiveGroupedFiles) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        if (!File.Exists(file.Source)) {
                            continue;
                        }
                        try {
                            switch (actionType) {
                                case ActionType.Copy:
                                    if (!file.Source.PathEquals(file.Target)) {
                                        if (File.Exists(file.Target)) {
                                            File.Delete(file.Target);
                                        }
                                        try {
                                            var buffer = new byte[1024 * 1024];
                                            using (var source = File.OpenRead(file.Source)) {
                                                long fileLength = source.Length;
                                                using (var dest = File.OpenWrite(file.Target)) {
                                                    long totalBytes = 0;
                                                    int currentBlockSize;
                                                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0) {
                                                        totalBytes += currentBlockSize;
                                                        dest.Write(buffer, 0, currentBlockSize);
                                                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(archiveGroupedFiles.Key, file.RelativePathInArchive, Math.Round((totalFilesDone + (double) totalBytes / fileLength) / totalFiles * 100, 2)));
                                                        _cancelToken?.ThrowIfCancellationRequested();
                                                    }
                                                }
                                            }
                                        } catch (OperationCanceledException) {
                                            // cleanup the potentially unfinished file copy
                                            if (File.Exists(file.Target)) {
                                                File.Delete(file.Target);
                                            }
                                            throw;
                                        }
                                    }
                                    break;
                                case ActionType.Move:
                                    if (!file.Source.PathEquals(file.Target)) {
                                        if (File.Exists(file.Target)) {
                                            File.Delete(file.Target);
                                        }
                                        File.Move(file.Source, file.Target);
                                    }
                                    break;
                                case ActionType.Delete:
                                    File.Delete(file.Source);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null);
                            }
                        } catch (OperationCanceledException) {
                            throw;
                        } catch (Exception e) {
                            throw new ArchiverException($"Failed to {actionType.ToString().ToLower()} {file.Source.PrettyQuote()}{(string.IsNullOrEmpty(file.Target) ? "" : $" in {file.Target.PrettyQuote()}")}.", e);
                        }
                        
                        totalFilesDone++;
                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(archiveGroupedFiles.Key, file.RelativePathInArchive));
                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(archiveGroupedFiles.Key, file.RelativePathInArchive, Math.Round((double) totalFilesDone / totalFiles * 100, 2)));
                    }

                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to {actionType.ToString().ToLower()} files{(string.IsNullOrEmpty(archiveGroupedFiles.Key) ? "" : $" in {archiveGroupedFiles.Key.PrettyQuote()}")}.", e);
                }

                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(archiveGroupedFiles.Key));
            }

            return totalFilesDone;
        }

        private struct FsFile {
            public string ArchivePath { get; set; }
            public string RelativePathInArchive { get; set; }
            public string Source { get; set; }
            public string Target { get; set; }
        }

        private enum ActionType {
            Copy,
            Move,
            Delete
        }
    }
}