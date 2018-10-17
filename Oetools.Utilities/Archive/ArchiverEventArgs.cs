#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ArchiverProgressionEventArgs.cs) is part of Oetools.Utilities.
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
    
    public class ArchiverEventArgs : IArchiverEventArgs {
        
        /// <inheritdoc cref="IArchiverEventArgs.EventType"/>
        public ArchiverEventType EventType { get; private set; }

        /// <inheritdoc cref="IArchiverEventArgs.ArchivePath"/>
        public string ArchivePath { get; private set; }

        /// <inheritdoc cref="IArchiverEventArgs.RelativePathInArchive"/>
        public string RelativePathInArchive { get; private set; }
        
        /// <inheritdoc cref="IArchiverEventArgs.PercentageDone"/>
        public double PercentageDone { get; private set; }
        
        internal static ArchiverEventArgs NewProcessedFile(string archivePath, string relativePathInArchive) {
            return new ArchiverEventArgs {
                ArchivePath = archivePath,
                EventType = ArchiverEventType.FileProcessed,
                RelativePathInArchive = relativePathInArchive
            };
        }
        
        internal static ArchiverEventArgs NewArchiveCompleted(string archivePath) {
            return new ArchiverEventArgs {
                ArchivePath = archivePath,
                EventType = ArchiverEventType.ArchiveCompleted
            };
        }
        
        internal static ArchiverEventArgs NewProgress(string archivePath, string currentRelativePathInArchive, double percentageDone) {
            return new ArchiverEventArgs {
                ArchivePath = archivePath,
                EventType = ArchiverEventType.GlobalProgression,
                PercentageDone = percentageDone,
                RelativePathInArchive = currentRelativePathInArchive
            };
        }
    }
    
}