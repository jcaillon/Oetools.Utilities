#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (Archiver.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Archive.Cab;
using Oetools.Utilities.Archive.Filesystem;
using Oetools.Utilities.Archive.Ftp;
using Oetools.Utilities.Archive.Prolib;
using Oetools.Utilities.Archive.Zip;

namespace Oetools.Utilities.Archive {
    
    /// <summary>
    /// An archiver allows CRUD operation on an archive, see <see cref="New"/> method to get an instance.
    /// </summary>
    public class Archiver {

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IArchiver New(ArchiverType type) {
            switch (type) {
                case ArchiverType.Cab:
                    return new CabArchiver();
                case ArchiverType.Zip:
                    return new ZipArchiver();
                case ArchiverType.Prolib:
                    throw new ArgumentException($"Use the method {nameof(NewProlibArchiver)} to get a new prolib archiver.");
                case ArchiverType.Ftp:
                    return new FtpArchiver();
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }    
        }

        /// <summary>
        /// Get a new instance of a prolib archiver.
        /// </summary>
        /// <returns></returns>
        public static IArchiver NewProlibArchiver(string dlcPath) {
            return new ProlibArchiver(dlcPath);
        }

        /// <summary>
        /// Get a new instance of a file system archiver.
        /// </summary>
        /// <returns></returns>
        public static IArchiver NewFileSystemArchiver() {
            return new FileSystemArchiver();
        }
        
    }
}