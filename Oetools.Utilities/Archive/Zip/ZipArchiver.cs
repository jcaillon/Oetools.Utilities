#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ZipArchiver.cs) is part of Oetools.Utilities.
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
using System.IO.Compression;
using System.Linq;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Oetools.Utilities.Archive.Zip {
    /// <summary>
    ///     Allows to pack files into zip
    /// </summary>
    internal class ZipArchiver : ArchiverBase, IArchiver {

        private CompressionLevel _compressionLevel = CompressionLevel.NoCompression;
       
        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) {
            switch (archiveCompressionLevel) {
                case ArchiveCompressionLevel.None:
                    _compressionLevel = CompressionLevel.NoCompression;
                    break;
                case ArchiveCompressionLevel.Fastest:
                    _compressionLevel = CompressionLevel.Fastest;
                    break;
                case ArchiveCompressionLevel.Optimal:
                    _compressionLevel = CompressionLevel.Optimal;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(archiveCompressionLevel), archiveCompressionLevel, null);
            }
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public int PackFileSet(IEnumerable<IFileToArchive> filesToPackIn) {
            var filesToPack = filesToPackIn.ToList();
            int totalFiles = filesToPack.Count;
            int totalFilesDone = 0;
            foreach (var zipGroupedFiles in filesToPack.GroupBy(f => f.ArchivePath)) {
                try {
                    CreateArchiveFolder(zipGroupedFiles.Key);
                    var zipMode = File.Exists(zipGroupedFiles.Key) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
                    using (var zip = ZipFile.Open(zipGroupedFiles.Key, zipMode)) {
                        foreach (var file in zipGroupedFiles) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            if (!File.Exists(file.SourcePath)) {
                                continue;
                            }
                            try {
                                zip.CreateEntryFromFile(file.SourcePath, file.RelativePathInArchive, _compressionLevel);
                            } catch (Exception e) {
                                throw new ArchiverException($"Failed to pack {file.SourcePath.PrettyQuote()} into {zipGroupedFiles.Key.PrettyQuote()} and relative archive path {file.RelativePathInArchive}.", e);
                            }
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(zipGroupedFiles.Key, file.RelativePathInArchive));
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(zipGroupedFiles.Key, file.RelativePathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to pack to {zipGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(zipGroupedFiles.Key));
            }
            return totalFilesDone;
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            if (!File.Exists(archivePath)) {
                return Enumerable.Empty<IFileInArchive>();
            }
            using (var archive = ZipFile.OpenRead(archivePath)) {
                return archive.Entries
                    .Select(entry => new FileInZip {
                        RelativePathInArchive = entry.FullName,
                        SizeInBytes = (ulong) entry.Length,
                        LastWriteTime = entry.LastWriteTime.DateTime,
                        ArchivePath = archivePath
                    } as IFileInArchive);
            }
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtractIn) {
            var filesToExtract = filesToExtractIn.ToList();
            int totalFiles = filesToExtract.Count;
            int totalFilesDone = 0;
            foreach (var zipGroupedFiles in filesToExtract.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(zipGroupedFiles.Key)) {
                    continue;
                }
                try {
                    // create all necessary extraction folders
                    foreach (var extractDirGroupedFiles in zipGroupedFiles.GroupBy(f => Path.GetDirectoryName(f.ExtractionPath))) {
                        if (!Directory.Exists(extractDirGroupedFiles.Key)) {
                            Directory.CreateDirectory(extractDirGroupedFiles.Key);
                        }
                    }
                    using (var zip = ZipFile.OpenRead(zipGroupedFiles.Key)) {
                        foreach (var entry in zip.Entries) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            var fileToExtract = zipGroupedFiles.FirstOrDefault(f => entry.FullName.PathEquals(f.RelativePathInArchive));
                            if (fileToExtract != null) {
                                try {
                                    entry.ExtractToFile(fileToExtract.ExtractionPath, true);
                                } catch (Exception e) {
                                    throw new ArchiverException($"Failed to extract {fileToExtract.ExtractionPath.PrettyQuote()} from {zipGroupedFiles.Key.PrettyQuote()} and relative archive path {fileToExtract.RelativePathInArchive}.", e);
                                }
                                totalFilesDone++;
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(zipGroupedFiles.Key, fileToExtract.RelativePathInArchive));
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(zipGroupedFiles.Key, fileToExtract.RelativePathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                            }
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to extract files from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(zipGroupedFiles.Key));
            }

            return totalFilesDone;
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDeleteIn) {            
            var filesToDelete = filesToDeleteIn.ToList();
            int totalFiles = filesToDelete.Count;
            int totalFilesDone = 0;
            foreach (var zipGroupedFiles in filesToDelete.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(zipGroupedFiles.Key)) {
                    continue;
                }
                try {
                    using (var zip = ZipFile.Open(zipGroupedFiles.Key, ZipArchiveMode.Update)) {
                        foreach (var entry in zip.Entries.ToList()) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            var fileToExtract = zipGroupedFiles.FirstOrDefault(f => entry.FullName.PathEquals(f.RelativePathInArchive));
                            if (fileToExtract != null) {
                                try {
                                    entry.Delete();
                                } catch (Exception e) {
                                    throw new ArchiverException($"Failed to delete {fileToExtract.RelativePathInArchive.PrettyQuote()} from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                                }
                                totalFilesDone++;
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(zipGroupedFiles.Key, fileToExtract.RelativePathInArchive));
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(zipGroupedFiles.Key, fileToExtract.RelativePathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                            }
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to delete files from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(zipGroupedFiles.Key));
            }

            return totalFilesDone;
        }
        
        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMoveIn) {
            var filesToMove = filesToMoveIn.ToList();
            int totalFiles = filesToMove.Count;
            int totalFilesDone = 0;
            
            foreach (var zipGroupedFiles in filesToMove.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(zipGroupedFiles.Key)) {
                    continue;
                }
                var tempPath = Path.Combine(Path.GetDirectoryName(zipGroupedFiles.Key) ?? Path.GetTempPath(), $"~{Path.GetRandomFileName()}");
                Utils.CreateDirectoryIfNeeded(tempPath, FileAttributes.Hidden);
                try {
                    using (var zip = ZipFile.Open(zipGroupedFiles.Key, ZipArchiveMode.Update)) {
                        foreach (var entry in zip.Entries.ToList()) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            var fileToMove = zipGroupedFiles.FirstOrDefault(f => entry.FullName.PathEquals(f.RelativePathInArchive));
                            if (fileToMove != null) {
                                try {
                                    var exportPath = Path.Combine(tempPath, "temp");
                                    entry.ExtractToFile(exportPath);
                                    entry.Delete();
                                    zip.CreateEntryFromFile(exportPath, fileToMove.NewRelativePathInArchive, _compressionLevel);
                                    File.Delete(exportPath);
                                } catch (Exception e) {
                                    throw new ArchiverException($"Failed to move {fileToMove.RelativePathInArchive.PrettyQuote()} to {fileToMove.NewRelativePathInArchive.PrettyQuote()} in {zipGroupedFiles.Key.PrettyQuote()}.", e);
                                }
                                totalFilesDone++;
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(zipGroupedFiles.Key, fileToMove.RelativePathInArchive));
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(zipGroupedFiles.Key, fileToMove.RelativePathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                            }
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to move files from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                } finally {
                    Directory.Delete(tempPath, true);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(zipGroupedFiles.Key));
            }

            return totalFilesDone;
        }
    }
}