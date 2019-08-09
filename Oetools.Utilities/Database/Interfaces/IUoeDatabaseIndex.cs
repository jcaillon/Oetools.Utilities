#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (IUoeDatabaseIndex.cs) is part of Oetools.Utilities.
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

using System.Collections.Generic;

namespace Oetools.Utilities.Openedge.Database.Interfaces {

    /// <summary>
    /// An openedge database index.
    /// </summary>
    public interface IUoeDatabaseIndex {
        /// <summary>
        /// Name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Is the index active?
        /// </summary>
        bool Active { get; set; }

        /// <summary>
        /// Is it a primary index?
        /// </summary>
        bool Primary { get; set; }

        /// <summary>
        /// Is it a unique index?
        /// </summary>
        bool Unique { get; set; }

        /// <summary>
        /// Is it a word index? (otherwise, binary).
        /// </summary>
        bool Word { get; set; }

        /// <summary>
        /// The index Crc.
        /// </summary>
        ushort Crc { get; set; }

        /// <summary>
        /// The database area in which this index is stored.
        /// </summary>
        string Area { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// A list of fields which are part of this index.
        /// </summary>
        IList<IUoeDatabaseIndexField> Fields { get; set; }
    }
}
