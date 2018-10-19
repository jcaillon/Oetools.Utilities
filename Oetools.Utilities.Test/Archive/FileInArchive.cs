using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Test.Archive {
    public class FileInArchive : IFileInArchiveToExtract, IFileInArchiveToDelete, IFileToArchive {
        public string ArchivePath { get; set; }
        public string RelativePathInArchive { get; set; }
        public string ExtractionPath { get; set; }
        public string SourcePath { get; set; }
    }
}