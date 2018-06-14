﻿using System;

namespace Oetools.Utilities.Archive.Prolib {
    public class ProlibFile : IFilePackaged {
        public string RelativePathInPack { get; set; }
        public long SizeInBytes { get; set; }
        public string Type { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime DateAdded { get; set; }
    }
}