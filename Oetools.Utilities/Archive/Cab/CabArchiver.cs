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
using System.Linq;
using System.Threading;
using CabinetManager;

namespace Oetools.Utilities.Archive.Cab {
    
    /// <summary>
    /// Allows CRUD operations on windows cabinet files.
    /// </summary>
    internal class CabArchiver : ArchiverBase, IArchiver {

        private CabCompressionLevel _compressionLevel = CabCompressionLevel.None;

        private readonly ICabManager _cabManager;
        
        internal CabArchiver() {
            _cabManager = CabManager.New();
        }

        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
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

        /// <inheritdoc cref="ISimpleArchiver.ArchiveFileSet"/>
        public int ArchiveFileSet(IEnumerable<IFileToArchive> filesToPack) {
            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                return _cabManager.PackFileSet(filesToPack.Select(f => CabFile.NewToPack(f.ArchivePath, f.RelativePathInArchive, f.SourcePath)));
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                return _cabManager.ListFiles(archivePath).Select(f => new FileInCab(f.CabPath, f.RelativePathInCab, f.SizeInBytes, f.LastWriteTime));
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtract) {
            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                return _cabManager.ExtractFileSet(filesToExtract.Select(f => CabFile.NewToExtract(f.ArchivePath, f.RelativePathInArchive, f.ExtractionPath)));
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDelete) {
            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                return _cabManager.DeleteFileSet(filesToDelete.Select(f => CabFile.NewToDelete(f.ArchivePath, f.RelativePathInArchive)));
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMove) {
            _cabManager.OnProgress += CabManagerOnProgress;
            try {
                return _cabManager.MoveFileSet(filesToMove.Select(f => CabFile.NewToMove(f.ArchivePath, f.RelativePathInArchive, f.NewRelativePathInArchive)));
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException(e.Message, e);
            } finally {
                _cabManager.OnProgress -= CabManagerOnProgress;
            }
        }

        private void CabManagerOnProgress(object sender, ICabProgressionEventArgs e) {
            switch (e.EventType) {
                case CabEventType.GlobalProgression:
                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(e.CabPath, e.RelativePathInCab, e.PercentageDone));
                    break;
                case CabEventType.FileProcessed:
                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(e.CabPath, e.RelativePathInCab));
                    break;
                case CabEventType.CabinetCompleted:
                    OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(e.CabPath));
                    break;
            }
        }
        
        private class CabFile : IFileInCabToDelete, IFileInCabToExtract, IFileToAddInCab, IFileInCabToMove {
            
            public string CabPath { get; private set; }
            public string RelativePathInCab { get; private set; }
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