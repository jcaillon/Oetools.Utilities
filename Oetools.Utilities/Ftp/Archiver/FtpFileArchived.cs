using System;

namespace Oetools.Utilities.Archive.Ftp {
    public class FtpFileArchived : IFileArchived {
        public string ArchivePath { get; set; }
        public string RelativePathInArchive { get; set; }
        public ulong SizeInBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
    }
}