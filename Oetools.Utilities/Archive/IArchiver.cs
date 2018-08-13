#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (IArchiver.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Archive {

    public interface IArchiver {
        
        /// <summary>
        /// Copy/compress files into archives
        /// </summary>
        /// <param name="files">List of files to archive</param>
        /// <param name="compressionLevel">The compression level used when creating the archive</param>
        /// <param name="progressHandler">Handler for receiving progress information; this may be null if progress is not desired</param>
        void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler);

        /// <summary>
        /// List all the files in an archive
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        List<IFileArchived> ListFiles(string archivePath);
        
        /*
        /// <summary>
        /// Extracts the given files from archives
        /// </summary>
        /// <param name="files"></param>
        /// <param name="progressHandler"></param>
        void ExtractFileSet(List<IFileToExtract> files, EventHandler<ArchiveProgressionEventArgs> progressHandler = null);
        */
    }

}