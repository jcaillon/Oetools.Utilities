#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabase.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Openedge.Database.Interfaces;

namespace Oetools.Utilities.Openedge.Database {

    /// <inheritdoc />
    public class UoeDatabase : IUoeDatabase {

        /// <inheritdoc />
        public DateTime ExtractionTime { get; set; }

        /// <inheritdoc />
        public string LogicalName { get; set; }

        /// <inheritdoc />
        public string PhysicalName { get; set; }

        /// <inheritdoc />
        public Version Version { get; set; }

        /// <inheritdoc />
        public DatabaseBlockSize BlockSize { get; set; }

        /// <inheritdoc />
        public string Charset { get; set; }

        /// <inheritdoc />
        public string Collation { get; set; }

        /// <inheritdoc />
        public virtual IList<IUoeDatabaseSequence> Sequences { get; set; }

        /// <inheritdoc />
        public virtual IList<IUoeDatabaseTable> Tables { get; set; }
    }
}
