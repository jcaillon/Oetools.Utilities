using System;
using System.Collections.Generic;

namespace Oetools.Utilities.Archive {

    public interface IPackager {
        
        /// <summary>
        ///     Compresses files into a pack, specifying the names used to store the files in the pack
        /// </summary>
        /// <param name="files">A mapping from internal file paths to external file paths.</param>
        /// <param name="compLevel">The compression level used when creating the pack</param>
        /// <param name="progressHandler">Handler for receiving progress information; this may be null if progress is not desired.</param>
        void PackFileSet(List<IFileToDeployInPackage> files, CompressionLvl compLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler);
    }

}