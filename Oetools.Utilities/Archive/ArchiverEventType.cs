#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ArchiverEventType.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Archive {
    
    /// <summary>
    /// An archiver event type.
    /// </summary>
    public enum ArchiverEventType {
        
        /// <summary>
        /// Published when the archiving process progresses, as the archive is written.
        /// </summary>
        /// <remarks>
        /// This event is published for each chunk of data written on the archive.
        /// The <see cref="IArchiverEventArgs.RelativePathInArchive"/> will indicate which file is currently processed.
        /// </remarks>
        GlobalProgression,
        
        /// <summary>
        /// Published when a file has been processed, this can be used to determine which files are actually processed.
        /// </summary>
        /// <remarks>
        /// This event does NOT mean the file is already stored in the archive.
        /// This is only to inform that the file has been processed and will be saved in the archive.
        /// Use the <see cref="GlobalProgression"/> to follow the actual writing.
        /// </remarks>
        FileProcessed,

        /// <summary>
        /// Published when an archive has been completed and is saved.
        /// </summary>      
        ArchiveCompleted
        
    }
}