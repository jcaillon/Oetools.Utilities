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
        /// Sets the compression level to use when archiving.
        /// </summary>
        /// <param name="compressionLevel"></param>
        void SetCompressionLevel(CompressionLvl compressionLevel);
        
        /// <summary>
        /// Event published when the archiving process is progressing.
        /// </summary>
        event EventHandler<ArchiveProgressionEventArgs> OnProgress;
        
        /// <summary>
        /// Pack files into archives
        /// </summary>
        /// <param name="files">List of files to archive</param>
        /// <exception cref="ArchiveException"></exception>
        void PackFileSet(IEnumerable<IFileToArchive> files);

        /// <summary>
        /// List all the files in an archive
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        /// <exception cref="ArchiveException"></exception>
        List<IFileArchived> ListFiles(string archivePath);
        
        /// <summary>
        /// Extracts the given files from archives
        /// </summary>
        /// <param name="files"></param>
        /// <exception cref="ArchiveException"></exception>
        void ExtractFileSet(IEnumerable<IFileToExtract> files);
        
        /// <summary>
        /// Deletes the given files from archives
        /// </summary>
        /// <param name="files"></param>
        /// <exception cref="ArchiveException"></exception>
        void DeleteFileSet(IEnumerable<IFileToExtract> files);
        
    }

}