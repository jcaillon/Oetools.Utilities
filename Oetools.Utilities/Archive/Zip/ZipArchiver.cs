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
using Oetools.Utilities.Lib.Extension;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Oetools.Utilities.Archive.Zip {
    /// <summary>
    ///     Allows to pack files into zip
    /// </summary>
    public class ZipArchiver : ArchiverBase, IArchiver {

        private CompressionLevel _compressionLevel = CompressionLevel.NoCompression;
       
        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(CompressionLvl compressionLevel) {
            switch (compressionLevel) {
                case CompressionLvl.None:
                    _compressionLevel = CompressionLevel.NoCompression;
                    break;
                case CompressionLvl.Fastest:
                    _compressionLevel = CompressionLevel.Fastest;
                    break;
                case CompressionLvl.Optimal:
                    _compressionLevel = CompressionLevel.Optimal;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(compressionLevel), compressionLevel, null);
            }
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiveProgressionEventArgs> OnProgress;
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public void PackFileSet(IEnumerable<IFileToArchive> filesToPack) {
            foreach (var zipGroupedFiles in filesToPack.GroupBy(f => f.ArchivePath)) {
                try {
                    CreateArchiveFolder(zipGroupedFiles.Key);
                    var zipMode = File.Exists(zipGroupedFiles.Key) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
                    using (var zip = ZipFile.Open(zipGroupedFiles.Key, zipMode)) {
                        foreach (var file in zipGroupedFiles) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            try {
                                zip.CreateEntryFromFile(file.SourcePath, file.RelativePathInArchive, _compressionLevel);
                            } catch (Exception e) {
                                throw new ArchiveException($"Failed to pack {file.SourcePath.PrettyQuote()} into {zipGroupedFiles.Key.PrettyQuote()} and relative archive path {file.RelativePathInArchive}.", e);
                            }
                            OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, zipGroupedFiles.Key, file.SourcePath, file.RelativePathInArchive));
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to pack to {zipGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, zipGroupedFiles.Key, null, null));
            }
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileArchived> ListFiles(string archivePath) {
            if (!File.Exists(archivePath)) {
                throw new ArchiveException($"The archive does not exist : {archivePath.PrettyQuote()}.");
            }
            using (var archive = ZipFile.OpenRead(archivePath)) {
                return archive.Entries
                    .Select(entry => new ZipFileArchived {
                        RelativePathInArchive = entry.FullName,
                        SizeInBytes = (ulong) entry.Length,
                        LastWriteTime = entry.LastWriteTime.DateTime,
                        ArchivePath = archivePath
                    } as IFileArchived);
            }
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public void ExtractFileSet(IEnumerable<IFileArchivedToExtract> filesToExtract) {
            foreach (var zipGroupedFiles in filesToExtract.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(zipGroupedFiles.Key)) {
                    throw new ArchiveException($"The archive does not exist : {zipGroupedFiles.Key.PrettyQuote()}.");
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
                            var fileToExtract = zipGroupedFiles.FirstOrDefault(f => f.RelativePathInArchive.Equals(entry.FullName));
                            if (fileToExtract != null) {
                                try {
                                    entry.ExtractToFile(fileToExtract.ExtractionPath, true);
                                } catch (Exception e) {
                                    throw new ArchiveException($"Failed to extract {fileToExtract.ExtractionPath.PrettyQuote()} from {zipGroupedFiles.Key.PrettyQuote()} and relative archive path {fileToExtract.RelativePathInArchive}.", e);
                                }
                                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, zipGroupedFiles.Key, fileToExtract.ExtractionPath, fileToExtract.RelativePathInArchive));
                            }
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to unpack files from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, zipGroupedFiles.Key, null, null));
            }
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public void DeleteFileSet(IEnumerable<IFileArchivedToDelete> filesToDelete) {
            foreach (var zipGroupedFiles in filesToDelete.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(zipGroupedFiles.Key)) {
                    throw new ArchiveException($"The archive does not exist : {zipGroupedFiles.Key.PrettyQuote()}.");
                }
                try {
                    using (var zip = ZipFile.Open(zipGroupedFiles.Key, ZipArchiveMode.Update)) {
                        foreach (var entry in zip.Entries.ToList()) {
                            _cancelToken?.ThrowIfCancellationRequested();
                            var fileToExtract = zipGroupedFiles.FirstOrDefault(f => f.RelativePathInArchive.Equals(entry.FullName));
                            if (fileToExtract != null) {
                                try {
                                    entry.Delete();
                                } catch (Exception e) {
                                    throw new ArchiveException($"Failed to delete {fileToExtract.RelativePathInArchive.PrettyQuote()} from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                                }
                                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, zipGroupedFiles.Key, null, fileToExtract.RelativePathInArchive));
                            }
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to delete files from {zipGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, zipGroupedFiles.Key, null, null));
            }
        }
    }
}