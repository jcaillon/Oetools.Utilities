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
using System.Globalization;
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Archive.Prolib {

    /// <summary>
    ///     Allows to delete files in a prolib file
    /// </summary>
    public class ProlibListing {
        
        #region Private

        private string _prolibExePath;
        private string _archivePath;

        #endregion
        
        #region Life and death

        public ProlibListing(string archivePath, string prolibExePath) {
            _archivePath = archivePath;
            _prolibExePath = prolibExePath;
        }

        #endregion

        #region Methods

        public List<ProlibFile> ListFiles() {
            using (var prolibExe = new ProcessIo(_prolibExePath)) {
                prolibExe.Arguments = _archivePath.Quoter() + " -list";
                if (!prolibExe.TryDoWait(true))
                    throw new Exception("Error while listing files from a .pl", new Exception(prolibExe.ErrorOutput.ToString()));

                var outputList = new List<ProlibFile>();
                var matches = new Regex(@"^(.+)\s+(\d+)\s+(\w+)\s+(\d+)\s+(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})\s(\d{2}\/\d{2}\/\d{2}\s\d{2}\:\d{2}\:\d{2})", RegexOptions.Multiline).Matches(prolibExe.StandardOutput.ToString());
                foreach (Match match in matches) {
                    var newFile = new ProlibFile {
                        RelativePathInPack = match.Groups[1].Value.TrimEnd(),
                        SizeInBytes = long.Parse(match.Groups[2].Value),
                        Type = match.Groups[3].Value,
                    };
                    if (DateTime.TryParseExact(match.Groups[5].Value, @"dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) {
                        newFile.DateAdded = date;
                    }
                    if (DateTime.TryParseExact(match.Groups[6].Value, @"dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) {
                        newFile.DateModified = date;
                    }
                }

                return outputList;
            }
        }

        #endregion

    }
}