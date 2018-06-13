﻿#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (CabPackager.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Oetools.Utilities.Archive.Compression;
using Oetools.Utilities.Archive.Compression.Cab;
using CompressionLevel = Oetools.Utilities.Archive.Compression.CompressionLevel;

namespace Oetools.Utilities.Archive.Cab {
    
    /// <summary>
    ///     Allows to pack files into a cab
    /// </summary>
    public class CabPackager : CabInfo, IPackager {
        
        public CabPackager(string path) : base(path) { }

        public void PackFileSet(IDictionary<string, IFileToDeployInPackage> files, CompressionLevel compLevel, EventHandler<ArchiveProgressEventArgs> progressHandler) {
            var filesDic = files.ToDictionary(kpv => kpv.Key, kpv => kpv.Value.From);
            PackFileSet(filesDic, compLevel, progressHandler);
        }
    }
}