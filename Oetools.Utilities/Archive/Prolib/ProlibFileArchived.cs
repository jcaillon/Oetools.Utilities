using System;

namespace Oetools.Utilities.Archive.Prolib {
    public class ProlibFileArchived : IFileArchived {
        public string PackPath { get; set; }
        public string RelativePathInPack { get; set; }
        public ulong SizeInBytes { get; set; }
        public string Type { get; set; }
        public DateTime LastWriteTime { get; set; }
        public DateTime DateAdded { get; set; }
    }
}