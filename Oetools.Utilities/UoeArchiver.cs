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

using System.Runtime.CompilerServices;
using DotUtilities.Archive;
using DotUtilities.Archive.HttpFileServer;
using DotUtilities.Archive.Zip;
using Oetools.Utilities.Archive.Cab;
using Oetools.Utilities.Archive.Prolib;
using Oetools.Utilities.Archive.Xcode;

[assembly: InternalsVisibleTo("Oetools.Utilities.Test")]

namespace Oetools.Utilities.Archive {

    /// <summary>
    /// An archiver allows CRUD operation on an archive.
    /// </summary>
    public static class UoeArchiver {

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static ICabArchiver NewCabArchiver() => new CabArchiver();

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IProlibArchiver NewProlibArchiver() => new ProlibArchiver();

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IXcodeArchiver NewXcodeArchiver() => new XcodeArchiver();

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IZipArchiver NewZipArchiver() => DotUtilities.Archiver.NewZipArchiver();

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IArchiverFullFeatured NewFtpArchiver() => DotUtilities.Archiver.NewFtpArchiver();

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IArchiverFullFeatured NewFileSystemArchiver() => DotUtilities.Archiver.NewFileSystemArchiver();

        /// <summary>
        /// Get a new instance of an archiver.
        /// </summary>
        /// <returns></returns>
        public static IHttpFileServerArchiver NewHttpFileServerArchiver() => DotUtilities.Archiver.NewHttpFileServerArchiver();

    }
}
