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
    public class CabPackager : IPackager {
        
        public void PackFileSet(List<IFileToDeployInPackage> files, CompressionLvl compLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            foreach (var cabGroupedFiles in files.GroupBy(f => f.PackPath)) {
                var cabInfo = new CabInfo(cabGroupedFiles.Key);
                var filesDic = cabGroupedFiles.ToDictionary(file => file.RelativePathInPack, file => file.From);
                cabInfo.PackFileSet(filesDic, CompressionLevel.None, (sender, args) => {
                    if (args.ProgressType == ArchiveProgressType.FinishFile) {
                        progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(args.CurrentFileName, args.TreatmentException, args.CannotCancel));
                    }
                });
            }
        }

        public List<IFilePackaged> ListFiles(string archivePath) {
            var cabInfo = new CabInfo(archivePath);
            return cabInfo.GetFiles().Select(info => new CabFilePackaged {
                RelativePathInPack = Path.Combine(info.Path, info.Name),
                SizeInBytes = info.Length,
                DateAdded = info.LastWriteTime,
                DateModified = info.LastWriteTime
            }).Cast<IFilePackaged>().ToList();
        }
    }
}