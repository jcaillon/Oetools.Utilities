#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabaseIndex.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Openedge.Database.Interfaces;

namespace Oetools.Utilities.Openedge.Database {

    /// <inheritdoc />
    public class UoeDatabaseIndex : IUoeDatabaseIndex {

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public bool Active { get; set; }

        /// <inheritdoc />
        public bool Primary { get; set; }

        /// <inheritdoc />
        public bool Unique { get; set; }

        /// <inheritdoc />
        public bool Word { get; set; }

        /// <inheritdoc />
        public ushort Crc { get; set; }

        /// <inheritdoc />
        public string Area { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public virtual IList<IUoeDatabaseIndexField> Fields { get; set; }

    }
}
