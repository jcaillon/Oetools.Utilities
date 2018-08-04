// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ZipPackager.cs) is part of csdeployer.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace Oetools.Utilities.Archive.Zip {
    /// <summary>
    ///     Allows to pack files into zip
    /// </summary>
    public class ZipArchiver : Archiver, IArchiver {
        public void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            foreach (var zipGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                //var zipInfo = new ZipInfo(cabGroupedFiles.Key);
                //var filesDic = cabGroupedFiles.ToDictionary(file => file.RelativePathInPack, file => file.From);
                //zipInfo.PackFileSet(filesDic, CompressionLevel.None, (sender, args) => {
                //    if (args.ProgressType == ArchiveProgressType.FinishFile) {
                //        progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(args.CurrentFileName, args.TreatmentException, args.CannotCancel));
                //    }
                //});
                try {
                    CreateArchiveFolder(zipGroupedFiles.Key);
                    var zipMode = File.Exists(zipGroupedFiles.Key) ? ZipArchiveMode.Update : ZipArchiveMode.Create;
                    using (var zip = ZipFile.Open(zipGroupedFiles.Key, zipMode)) {
                        foreach (var file in zipGroupedFiles) {
                            try {
                                zip.CreateEntryFromFile(file.SourcePath, file.RelativePathInArchive, CompressionLevel.Fastest);
                            } catch (Exception e) {
                                progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, zipGroupedFiles.Key, file.RelativePathInArchive, e));
                            }
                        }
                    }
                } catch (Exception e) {
                    progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, zipGroupedFiles.Key, null, e));
                }
            }
        }

        public List<IFileArchived> ListFiles(string archivePath) {
            using (var archive = ZipFile.OpenRead(archivePath)) {
                return archive.Entries
                    .Select(entry => new ZipFileArchived {
                        RelativePathInArchive = entry.FullName,
                        SizeInBytes = (ulong) entry.Length,
                        LastWriteTime = entry.LastWriteTime.DateTime,
                        ArchivePath = archivePath
                    } as IFileArchived)
                    .ToList();
            }
        }
    }
}