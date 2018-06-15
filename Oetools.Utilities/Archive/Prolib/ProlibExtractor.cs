#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProlibExtractor.cs) is part of csdeployer.
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
using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Archive.Prolib {

    /// <summary>
    ///     Allows to delete files in a prolib file
    /// </summary>
    public class ProlibExtractor {
        
        #region Private

        private string _prolibExePath;
        private string _archivePath;
        private string _plExtractionFolder;

        #endregion
        
        #region Life and death

        public ProlibExtractor(string archivePath, string prolibExePath, string plExtractionFolder) {
            _archivePath = archivePath;
            _plExtractionFolder = plExtractionFolder;
            _prolibExePath = prolibExePath;
        }

        #endregion

        #region Methods


        #endregion

    }
}