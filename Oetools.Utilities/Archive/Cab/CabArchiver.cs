#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (CabArchiver.cs) is part of Oetools.Utilities.
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
using System.Threading;
using CabinetManager;
using DotUtilities;
using DotUtilities.Archive;
using DotUtilities.Extensions;

namespace Oetools.Utilities.Archive.Cab {

    /// <summary>
    /// Allows CRUD operations on windows cabinet files.
    /// </summary>
    internal class CabArchiver : ArchiverBase, ICabArchiver {

        private CabCompressionLevel _compressionLevel = CabCompressionLevel.None;

        private readonly ICabManager _cabManager;

        internal CabArchiver() {
            _cabManager = CabManager.New();
        }

        /// <inheritdoc />
        public int CheckFileSet(IEnumerable<IFileInArchiveToCheck> filesToCheck) {
            int total = 0;
            foreach (var groupedFiles in filesToCheck.ToNonNullEnumerable().GroupBy(f => f.ArchivePath)) {
                try {
                    _cancelToken?.ThrowIfCancellationRequested();
                    HashSet<string> list = null;
                    if (File.Exists(groupedFiles.Key)) {
                        list = _cabManager.ListFiles(groupedFiles.Key).Select(f => f.RelativePathInCab.ToCleanRelativePathWin()).ToHashSet(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    }
                    foreach (var file in groupedFiles) {
                        if (list != null && list.Contains(file.PathInArchive.ToCleanRelativePathWin())) {
                            file.Processed = true;
                            total++;
                        } else {
                            file.Processed = false;
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException(e.Message, e);
                }
            }
            return total;
        }

        /// <inheritdoc cref="ICabArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) {
            // TODO : switch compression level when it is implemented in CabinetManager
            // does nothing for now because the compression is not implemented yet
            switch (archiveCompressionLevel) {
                case ArchiveCompressionLevel.None:
                    _compressionLevel = CabCompressionLevel.None;
                    break;
                case ArchiveCompressionLevel.Fastest:
                    _compressionLevel = CabCompressionLevel.None;
                    break;
                case ArchiveCompressionLevel.Optimal:
                    _compressionLevel = CabCompressionLevel.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(archiveCompressionLevel), archiveCompressionLevel, null);
            }
            _cabManager.SetCompressionLevel(_compressionLevel);
        }

        /// <inheritdoc cref="ArchiverBase.SetCancellationToken"/>
        public override void SetCancellationToken(CancellationToken? cancelToken) {
            base.SetCancellationToken(cancelToken);
            _cabManager.SetCancellationToken(_cancelToken);
        }

        /// <inheritdoc cref="IArchiver.ArchiveFileSet"/>
        public int ArchiveFileSet(IEnumerable<IFileToArchive> filesToArchive) {
            return Do(filesToArchive, Action.Archive);
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;

        /// <inheritdoc cref="IArchiverList.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                return (_cabManager.ListFiles(archivePath)?.Select(f => new FileInCab(f.CabPath, f.RelativePathInCab.ToCleanRelativePathWin(), f.SizeInBytes, f.LastWriteTime))).ToNonNullEnumerable();
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        /// <inheritdoc cref="IArchiverExtract.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtract) {
            return Do(filesToExtract, Action.Extract);
        }

        /// <inheritdoc cref="IArchiverDelete.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDelete) {
            return Do(filesToDelete, Action.Delete);
        }

        /// <inheritdoc cref="IArchiverMove.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMove) {
            return Do(filesToMove, Action.Move);
        }

        private int Do(IEnumerable<IFileArchivedBase> filesIn, Action action) {
            if (filesIn == null) {
                return 0;
            }

            var files = filesIn.ToList();
            files.ForEach(f => f.Processed = false);

            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                List<CabFile> parameterFiles;
                int nbFilesProcessed;

                switch (action) {
                    case Action.Archive:
                        parameterFiles = files.Select(f => CabFile.NewToPack(f.ArchivePath, f.PathInArchive.ToCleanRelativePathWin(), ((IFileToArchive) f).SourcePath)).ToList();
                        nbFilesProcessed = _cabManager.PackFileSet(parameterFiles);
                        break;
                    case Action.Extract:
                        parameterFiles = files.Select(f => CabFile.NewToExtract(f.ArchivePath, f.PathInArchive.ToCleanRelativePathWin(), ((IFileInArchiveToExtract) f).ExtractionPath)).ToList();
                        nbFilesProcessed = _cabManager.ExtractFileSet(parameterFiles);
                        break;
                    case Action.Delete:
                        parameterFiles = files.Select(f => CabFile.NewToDelete(f.ArchivePath, f.PathInArchive.ToCleanRelativePathWin())).ToList();
                        nbFilesProcessed = _cabManager.DeleteFileSet(parameterFiles);
                        break;
                    case Action.Move:
                        parameterFiles = files.Select(f => CabFile.NewToMove(f.ArchivePath, f.PathInArchive.ToCleanRelativePathWin(), ((IFileInArchiveToMove) f).NewRelativePathInArchive)).ToList();
                        nbFilesProcessed = _cabManager.MoveFileSet(parameterFiles);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action), action, null);
                }

                for (int i = 0; i < parameterFiles.Count; i++) {
                    files[i].Processed = parameterFiles[i].Processed;
                }

                return nbFilesProcessed;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        private void CabManagerOnProgress(object sender, ICabProgressionEventArgs e) {
            if (e.EventType == CabEventType.GlobalProgression) {
                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(e.CabPath, e.RelativePathInCab, e.PercentageDone));
            }
        }

        private enum Action {
            Archive,
            Extract,
            Delete,
            Move
        }

        private class CabFile : IFileInCabToDelete, IFileInCabToExtract, IFileToAddInCab, IFileInCabToMove {

            public string CabPath { get; private set; }
            public string RelativePathInCab { get; private set; }
            public bool Processed { get; set; }
            public string ExtractionPath { get; private set; }
            public string SourcePath { get; private set; }
            public string NewRelativePathInCab { get; private set; }

            public static CabFile NewToPack(string cabPath, string relativePathInCab, string sourcePath) {
                return new CabFile {
                    CabPath = cabPath,
                    RelativePathInCab = relativePathInCab,
                    SourcePath = sourcePath
                };
            }

            public static CabFile NewToExtract(string cabPath, string relativePathInCab, string extractionPath) {
                return new CabFile {
                    CabPath = cabPath,
                    RelativePathInCab = relativePathInCab,
                    ExtractionPath = extractionPath
                };
            }

            public static CabFile NewToDelete(string cabPath, string relativePathInCab) {
                return new CabFile {
                    CabPath = cabPath,
                    RelativePathInCab = relativePathInCab
                };
            }

            public static CabFile NewToMove(string cabPath, string relativePathInCab, string newRelativePathInCab) {
                return new CabFile {
                    CabPath = cabPath,
                    RelativePathInCab = relativePathInCab,
                    NewRelativePathInCab = newRelativePathInCab
                };
            }

        }

    }
}
