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

namespace Oetools.Utilities.Archive.Xcode {

    /// <summary>
    /// A simple archiver to encode/decode files using the openedge xcode algorithm.
    /// </summary>
    public interface IXcodeArchiver : IArchiver {

        /// <summary>
        /// Sets the encode mode. If true, the archiver will encode, if false it will decode. Default to true.
        /// </summary>
        /// <param name="isEncodeMode"></param>
        void SetEncodeMode(bool isEncodeMode);

        /// <summary>
        /// Use the given encryption key.
        /// </summary>
        /// <param name="key">If null, will default to Progress.</param>
        void SetKey(string key);
    }
}
