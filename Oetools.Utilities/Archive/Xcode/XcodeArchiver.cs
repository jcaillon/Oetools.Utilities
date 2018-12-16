#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (XcodeArchiver.cs) is part of Oetools.Utilities.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Oetools.Utilities.Openedge;

namespace Oetools.Utilities.Archive.Xcode {
    
    /// <inheritdoc cref="IXcodeArchiver"/>
    public class XcodeArchiver : UoeEncryptor, IXcodeArchiver {
        
        public XcodeArchiver() : base(null) { }
        
        private CancellationToken? _cancelToken;
        private bool _encode = true;
        
        /// <inheritdoc cref="IXcodeArchiver.SetEncodeMode"/>
        public void SetEncodeMode(bool isEncodeMode) {
            _encode = isEncodeMode;
        }
        
        /// <inheritdoc cref="IArchiver.SetCancellationToken"/>
        public void SetCancellationToken(CancellationToken? cancelToken) {
            _cancelToken = cancelToken;
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;
        
        /// <inheritdoc cref="IArchiver.ArchiveFileSet"/>
        public int ArchiveFileSet(IEnumerable<IFileToArchive> filesToArchiveIn) {
            if (filesToArchiveIn == null) {
                return 0;
            }
            
            var fileToArchive = filesToArchiveIn.ToList();
            fileToArchive.ForEach(f => f.Processed = false);
            
            int totalFiles = fileToArchive.Count;
            int totalFilesDone = 0;
            try {
                foreach (var file in fileToArchive) {
                    _cancelToken?.ThrowIfCancellationRequested();
                    if (!File.Exists(file.SourcePath)) {
                        continue;
                    }
                    ConvertFile(file.SourcePath, _encode, Path.Combine(file.ArchivePath ?? "", file.PathInArchive));
                    totalFilesDone++;
                    file.Processed = true;
                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(file.ArchivePath, file.PathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception e) {
                throw new ArchiverException($"Failed to archive. {e.Message}", e);
            }
            return totalFilesDone;
        }
    }
}