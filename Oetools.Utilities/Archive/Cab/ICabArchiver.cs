#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (IProlibArchiver.cs) is part of Oetools.Utilities.
//
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using DotUtilities.Archive;

namespace Oetools.Utilities.Archive.Cab {
    public interface ICabArchiver : IArchiverFullFeatured {

        /// <summary>
        /// Sets the compression level to use for the next <see cref="IArchiver.ArchiveFileSet"/> process.
        /// </summary>
        /// <param name="archiveCompressionLevel"></param>
        void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel);
    }
}
