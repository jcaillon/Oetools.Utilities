using System;
using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Ftp.Archiver {
    public class FtpFileArchived : IFileArchived {
        public string ArchivePath { get; set; }
        public string RelativePathInArchive { get; set; }
        public ulong SizeInBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
    }
}