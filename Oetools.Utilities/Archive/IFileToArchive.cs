namespace Oetools.Utilities.Archive {
    public interface IFileToArchive {
        
        /// <summary>
        /// The path of the file to archive
        /// </summary>
        string SourcePath { get; set; }

        /// <summary>
        /// The path to the archive in which to put this file
        /// </summary>
        string ArchivePath { get; set; }

        /// <summary>
        /// The relative path of this file inside the archive
        /// </summary>
        string RelativePathInArchive { get; set; }
    }
}