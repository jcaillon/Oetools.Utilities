using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Test.Archive {
    public class FileToExtract : IFileToExtract {
        public string ArchivePath { get; set; }
        public string RelativePathInArchive { get; set; }
        public string ExtractionPath { get; set; }
    }
}