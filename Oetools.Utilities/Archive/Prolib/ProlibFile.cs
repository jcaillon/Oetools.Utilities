using System;

namespace Oetools.Utilities.Archive.Prolib {
    public class ProlibFile {
        public string RelativePathInPack { get; set; }
        public int SizeInBytes { get; set; }
        public string Type{ get; set; }
        public int Offset { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime DateAdded { get; set; }
    }
}