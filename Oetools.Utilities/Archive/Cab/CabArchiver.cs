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
using Oetools.Utilities.Archive.Compression;
using Oetools.Utilities.Archive.Compression.Cab;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Archive.Cab {
    
    /// <summary>
    ///     Allows to pack files into a cab
    /// </summary>
    public class CabArchiver : Archiver, IArchiver {

        private CompressionLevel _compressionLevel = CompressionLevel.None;

        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(CompressionLvl compressionLevel) {
            switch (compressionLevel) {
                case CompressionLvl.None:
                    _compressionLevel = CompressionLevel.None;
                    break;
                case CompressionLvl.Fastest:
                    _compressionLevel = CompressionLevel.Min;
                    break;
                case CompressionLvl.Optimal:
                    _compressionLevel = CompressionLevel.Max;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(compressionLevel), compressionLevel, null);
            }
        }
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public void PackFileSet(IEnumerable<IFileToArchive> files) {
            foreach (var cabGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    CreateArchiveFolder(cabGroupedFiles.Key);
                    var cabInfo = new CabInfo(cabGroupedFiles.Key);
                    var filesDic = cabGroupedFiles.ToDictionary(file => file.RelativePathInArchive, file => file.SourcePath);
                    cabInfo.PackFileSet(filesDic, _compressionLevel, (sender, args) => {
                        if (args.ProgressType == ArchiveProgressType.FinishFile) {
                            OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, args.CurrentArchiveName, null, args.CurrentFileName));
                        }
                        if (args.TreatmentException != null) {
                            throw new ArchiveException($"Failed to pack into {args.CurrentArchiveName.PrettyQuote()} and relative archive path {args.CurrentFileName}.", args.TreatmentException);
                        }
                    });
                } catch (Exception e) {
                    throw new ArchiveException($"Failed to pack to {cabGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, cabGroupedFiles.Key, null, null));
            }
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiveProgressionEventArgs> OnProgress;

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public List<IFileArchived> ListFiles(string archivePath) {
            return new CabInfo(archivePath)
                .GetFiles()
                .Select(info => new CabFileArchived {
                    RelativePathInArchive = Path.Combine(info.Path, info.Name),
                    SizeInBytes = (ulong) info.Length,
                    LastWriteTime = info.LastWriteTime,
                    ArchivePath = archivePath
                } as IFileArchived)
                .ToList();
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public void ExtractFileSet(IEnumerable<IFileToExtract> files) {
            throw new NotImplementedException();
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public void DeleteFileSet(IEnumerable<IFileToExtract> files) {
            throw new NotImplementedException();
        }

    }
}