using System;

namespace Oetools.Utilities.Archive.Cab {
    public class CabFilePackaged : IFilePackaged {
        public string RelativePathInPack { get; set; }
        public long SizeInBytes { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime DateAdded { get; set; }
    }
}