#region header

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

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Oetools.Utilities.Archive.Compression;
using Oetools.Utilities.Archive.Compression.Zip;
using CompressionLevel = Oetools.Utilities.Archive.Compression.CompressionLevel;

namespace Oetools.Utilities.Archive.Zip {
    /// <summary>
    ///     Allows to pack files into zip
    /// </summary>
    public class ZipPackager : ZipInfo, IPackager {
        public ZipPackager(string path) : base(path) { }

        public void PackFileSet(IDictionary<string, IFileToDeployInPackage> files, CompressionLevel compLevel, EventHandler<ArchiveProgressEventArgs> progressHandler) {
            var filesDic = files.ToDictionary(kpv => kpv.Key, kpv => kpv.Value.From);
            PackFileSet(filesDic, compLevel, progressHandler);
            
            // TODO : use ZipFile/ZipArchive... introduce in 4.5
            
            //using (ZipArchive newFile = ZipFile.Open(zipName, ZipArchiveMode.Create)) // ZipArchiveMode.Update
            //{
            //    //Here are two hard-coded files that we will be adding to the zip
            //    //file.  If you don't have these files in your system, this will
            //    //fail.  Either create them or change the file names.
            //    newFile.CreateEntryFromFile(@"C:\Temp\File1.txt", "File1.txt");
            //    newFile.CreateEntryFromFile(@"C:\Temp\File2.txt", "File2.txt", CompressionLevel.Fastest);
            //}
        }
    }
}