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