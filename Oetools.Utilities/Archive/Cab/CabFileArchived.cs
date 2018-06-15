using System;

namespace Oetools.Utilities.Archive.Cab {
    public class CabFileArchived : IFileArchived {
        public string PackPath { get; set; }
        public string RelativePathInPack { get; set; }
        public ulong SizeInBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
    }
}