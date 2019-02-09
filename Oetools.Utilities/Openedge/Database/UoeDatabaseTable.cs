#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeTable.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Represents an openedge table.
    /// </summary>
    public class UoeDatabaseTable {

        /// <summary>
        /// Table name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Dump name.
        /// </summary>
        public string DumpName { get; set; }

        /// <summary>
        /// Crc 16 value.
        /// </summary>
        public ushort Crc { get; set; }

        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Label string atttribute.
        /// </summary>
        /// <remarks>
        /// L: left justify (default)
        /// C: center
        /// R: right
        /// T: remove trailing spaces
        /// U: do not translate the string
        /// ###: number from 1 to 999 mas amount of space
        /// </remarks>
        public string LabelAttribute { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Is the table hidden?
        /// </summary>
        public bool Hidden { get; set; }

        /// <summary>
        /// Is the table frozen?
        /// </summary>
        /// <remarks>let it go... let it goooOOOOo</remarks>
        public bool Frozen { get; set; }

        /// <summary>
        /// The database area in which this table is stored.
        /// </summary>
        public string Area { get; set; }

        /// <summary>
        /// Table type.
        /// </summary>
        public UoeDatabaseTableType Type { get; set; }

        /// <summary>
        /// Validation expression.
        /// </summary>
        public string ValidationExpression { get; set; }

        /// <summary>
        /// Validation message.
        /// </summary>
        public string ValidationMessage { get; set; }

        /// <summary>
        /// Validation message attribute.
        /// </summary>
        /// <inheritdoc cref="LabelAttribute"/>
        public string ValidationMessageAttribute { get; set; }

        /// <summary>
        /// The table fields.
        /// </summary>
        public virtual IList<UoeDatabaseField> Fields { get; set; }

        /// <summary>
        /// The table triggers.
        /// </summary>
        public virtual IList<UoeDatabaseTrigger> Triggers { get; set; }

        /// <summary>
        /// The table triggers.
        /// </summary>
        public virtual IList<UoeDatabaseIndex> Indexes { get; set; }
    }
}
