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
using Oetools.Utilities.Archive.Prolib.Core;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Archive.Prolib {
    
    /// <summary>
    /// In that case, a folder on the file system represents an archive.
    /// </summary>
    internal class ProlibArchiver : ArchiverBase, IProlibArchiver {

        private string _codePage;
        private ProLibraryVersion _version;
        private ProlibVersion _prolibVersion = ProlibVersion.Default;
        
        /// <inheritdoc cref="IProlibArchiver.SetProlibVersion"/>
        public void SetProlibVersion(ProlibVersion version) {
            _prolibVersion = version;
            if (version == ProlibVersion.Default) {
                return;
            }
            var val = (byte) version;
            if (!Enum.IsDefined(typeof(ProLibraryVersion), version)) {
                throw new ProLibraryException($"Unknown library version {version}.");
            }
            _version = (ProLibraryVersion) val;
        }

        /// <inheritdoc cref="IProlibArchiver.SetFilePathCodePage"/>
        public void SetFilePathCodePage(string codePage) {
            _codePage = codePage;
        }

        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) {
            // not applicable
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;
        
        /// <inheritdoc cref="ISimpleArchiver.ArchiveFileSet"/>
        public int ArchiveFileSet(IEnumerable<IFileToArchive> filesToPack) {
            int nbFilesProcessed = 0;
            foreach (var plGroupedFiles in filesToPack.GroupBy(f => f.ArchivePath)) {
                try {                
                    using (var proLibrary = new ProLibrary(plGroupedFiles.Key, _cancelToken)) {
                        if (_prolibVersion != ProlibVersion.Default) {
                            proLibrary.Version = _version;
                        }
                        proLibrary.CodePageName = _codePage;
                        proLibrary.OnProgress += OnProgressionEvent;
                        try {
                            foreach (var fileToArchive in plGroupedFiles) {
                                if (!File.Exists(fileToArchive.SourcePath)) {
                                    continue;
                                }
                                var fileRelativePath = fileToArchive.RelativePathInArchive.ToCleanRelativePathUnix();
                                proLibrary.AddExternalFile(fileToArchive.SourcePath, fileRelativePath);
                                nbFilesProcessed++;
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(plGroupedFiles.Key, fileRelativePath));
                            }
                            proLibrary.Save();
                        } finally {
                            proLibrary.OnProgress -= OnProgressionEvent;
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to pack to {plGroupedFiles.Key}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(plGroupedFiles.Key));
            }
            return nbFilesProcessed;
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            if (!File.Exists(archivePath)) {
                return Enumerable.Empty<IFileInArchive>();
            }
            using (var proLibrary = new ProLibrary(archivePath, _cancelToken)) {
                return proLibrary.Files
                    .Select(file => new FileInProlib {
                        ArchivePath = archivePath,
                        RelativePathInArchive = file.RelativePath,
                        LastWriteTime = file.LastWriteTime ?? DateTime.Now,
                        SizeInBytes = file.Size,
                        IsRcode = file.Type == ProLibraryFileType.Rcode,
                        DateAdded = file.PackTime ?? DateTime.Now
                    } as IFileInArchive);
            }
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtract) {
            int nbFilesProcessed = 0;
            foreach (var plGroupedFiles in filesToExtract.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(plGroupedFiles.Key)) {
                    continue;
                }
                try {      
                    // create all necessary extraction folders
                    foreach (var extractDirGroupedFiles in plGroupedFiles.GroupBy(f => Path.GetDirectoryName(f.ExtractionPath))) {
                        if (!Directory.Exists(extractDirGroupedFiles.Key) && !string.IsNullOrWhiteSpace(extractDirGroupedFiles.Key)) {
                            Directory.CreateDirectory(extractDirGroupedFiles.Key);
                        }
                    }
                    using (var proLibrary = new ProLibrary(plGroupedFiles.Key, _cancelToken)) {
                        proLibrary.OnProgress += OnProgressionEvent;
                        try {
                            foreach (var fileInArchiveToExtract in plGroupedFiles) {
                                var fileRelativePath = fileInArchiveToExtract.RelativePathInArchive.ToCleanRelativePathUnix();
                                if (proLibrary.ExtractToFile(fileRelativePath, fileInArchiveToExtract.ExtractionPath)) {
                                    nbFilesProcessed++;
                                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(plGroupedFiles.Key, fileRelativePath));
                                }
                            }
                        } finally {
                            proLibrary.OnProgress -= OnProgressionEvent;
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to extract files from {plGroupedFiles.Key}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(plGroupedFiles.Key));
            }
            return nbFilesProcessed;
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDelete) {
            int nbFilesProcessed = 0;
            foreach (var plGroupedFiles in filesToDelete.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(plGroupedFiles.Key)) {
                    continue;
                }
                try {                
                    using (var proLibrary = new ProLibrary(plGroupedFiles.Key, _cancelToken)) {
                        if (_prolibVersion != ProlibVersion.Default) {
                            proLibrary.Version = _version;
                        }
                        proLibrary.CodePageName = _codePage;
                        proLibrary.OnProgress += OnProgressionEvent;
                        try {
                            foreach (var fileToAddInCab in plGroupedFiles) {
                                var fileRelativePath = fileToAddInCab.RelativePathInArchive.ToCleanRelativePathUnix();
                                if (proLibrary.DeleteFile(fileRelativePath)) {
                                    nbFilesProcessed++;
                                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(plGroupedFiles.Key, fileRelativePath));
                                }
                            }
                            proLibrary.Save();
                        } finally {
                            proLibrary.OnProgress -= OnProgressionEvent;
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to delete files from {plGroupedFiles.Key}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(plGroupedFiles.Key));
            }
            return nbFilesProcessed;
        }

        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMove) {
            int nbFilesProcessed = 0;
            foreach (var plGroupedFiles in filesToMove.GroupBy(f => f.ArchivePath)) {
                if (!File.Exists(plGroupedFiles.Key)) {
                    continue;
                }
                try {                
                    using (var proLibrary = new ProLibrary(plGroupedFiles.Key, _cancelToken)) {
                        if (_prolibVersion != ProlibVersion.Default) {
                            proLibrary.Version = _version;
                        }
                        proLibrary.CodePageName = _codePage;
                        proLibrary.OnProgress += OnProgressionEvent;
                        try {
                            foreach (var fileToAddInCab in plGroupedFiles) {
                                var fileRelativePath = fileToAddInCab.RelativePathInArchive.ToCleanRelativePathUnix();
                                if (proLibrary.MoveFile(fileRelativePath, fileToAddInCab.NewRelativePathInArchive.ToCleanRelativePathUnix())) {
                                    nbFilesProcessed++;
                                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(plGroupedFiles.Key, fileRelativePath));
                                }
                            }
                            proLibrary.Save();
                        } finally {
                            proLibrary.OnProgress -= OnProgressionEvent;
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to move files from {plGroupedFiles.Key}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(plGroupedFiles.Key));
            }
            return nbFilesProcessed;
        }
        
        private void OnProgressionEvent(object sender, ProLibrarySaveEventArgs e) {
            if (sender is ProLibrary library) {
                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(library.FilePath, e.RelativePathInPl, Math.Round(e.TotalBytesDone / (double) e.TotalBytesToProcess * 100, 2)));
            }
        }

    }
}