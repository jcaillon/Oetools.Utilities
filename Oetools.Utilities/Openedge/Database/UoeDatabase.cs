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

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Represents an openedge database.
    /// </summary>
    public class UoeDatabase {

        /// <summary>
        /// The datetime at which the database info was extracted.
        /// </summary>
        public DateTime ExtractionTime { get; set; }

        /// <summary>
        /// The logical name of the database at the moment it was extracted.
        /// </summary>
        public string LogicalName { get; set; }

        /// <summary>
        /// The physical name of the database at the moment if was extracted.
        /// </summary>
        public string PhysicalName { get; set; }

        /// <summary>
        /// The version of the database (format major.minor).
        /// </summary>
        /// <remarks>
        /// _DbStatus._DbStatus-DbVers._DbStatus._DbStatus-DbVersMinor
        /// </remarks>
        public Version Version { get; set; }

        /// <summary>
        /// The database codepage.
        /// </summary>
        public string CodePage { get; set; }

        /// <summary>
        /// The database sequences.
        /// </summary>
        public virtual IList<UoeDatabaseSequence> Sequences { get; set; }

        /// <summary>
        /// The database tables.
        /// </summary>
        public virtual IList<UoeDatabaseTable> Tables { get; set; }
    }
}
