using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Test.Archive {
    public class FileArchived : IFileArchivedToExtract, IFileArchivedToDelete, IFileToArchive {
        public string ArchivePath { get; set; }
        public string RelativePathInArchive { get; set; }
        public string ExtractionPath { get; set; }
        public string SourcePath { get; set; }
    }
}