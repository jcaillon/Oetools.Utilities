#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (CabPackager.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using csdeployer.Lib.Compression;
using csdeployer.Lib.Compression.Cab;

namespace Oetools.Utilities.Archive.Cab {
    
    /// <summary>
    ///     Allows to pack files into a cab
    /// </summary>
    public class CabArchiver : Archiver, IArchiver {
        
        public void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            foreach (var cabGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    CreateArchiveFolder(cabGroupedFiles.Key);
                    var cabInfo = new CabInfo(cabGroupedFiles.Key);
                    var filesDic = cabGroupedFiles.ToDictionary(file => file.RelativePathInArchive, file => file.SourcePath);
                    cabInfo.PackFileSet(filesDic, CompressionLevel.None, (sender, args) => {
                        if (args.ProgressType == ArchiveProgressType.FinishFile) {
                            progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, args.CurrentArchiveName, args.CurrentFileName, args.TreatmentException));
                        }
                    });
                } catch (Exception e) {
                    progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, cabGroupedFiles.Key, null, e));
                }
            }
        }

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
    }
}