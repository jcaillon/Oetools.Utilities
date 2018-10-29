#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (CfSaveProgression.cs) is part of CabinetManager.
// 
// CabinetManager is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CabinetManager is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with CabinetManager. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;

namespace Oetools.Utilities.Archive.Prolib.Core {
    
    internal class ProLibrarySaveEventArgs : EventArgs {
        
        public string RelativePathInCab { get; private set; }

        public long TotalBytesDone { get; set; }
        
        public long TotalBytesToProcess { get; set; }

        public static ProLibrarySaveEventArgs New(string relativePathInCab, long totalBytesDone, long totalBytesToProcess) {
            return new ProLibrarySaveEventArgs {
                RelativePathInCab = relativePathInCab,
                TotalBytesDone = totalBytesDone,
                TotalBytesToProcess = totalBytesToProcess
            };
        }
    }
}