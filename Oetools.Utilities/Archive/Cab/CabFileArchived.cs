using System;

namespace Oetools.Utilities.Archive.Cab {
    public class CabFileArchived : IFileArchived {
        public string ArchivePath { get; set; }
        public string RelativePathInArchive { get; set; }
        public ulong SizeInBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
    }
}