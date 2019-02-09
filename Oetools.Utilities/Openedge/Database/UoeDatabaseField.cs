#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeField.cs) is part of Oetools.Utilities.
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
    /// Represents an openedge database field.
    /// </summary>
    public class UoeDatabaseField {

        /// <summary>
        /// Field name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Field type.
        /// </summary>
        public UoeDatabaseDataType DataType { get; set; }

        /// <summary>
        /// Format.
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Format attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        public string FormatAttribute { get; set; }

        /// <summary>
        /// Order (increment 10 by 10).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Position (stars at 2).
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Is field mandatory?
        /// </summary>
        public bool Mandatory { get; set; }

        /// <summary>
        /// Is field case sensitive?
        /// </summary>
        public bool CaseSensitive { get; set; }

        /// <summary>
        /// Extent (is the field an array, it is the size of this array).
        /// </summary>
        public int Extent { get; set; }

        /// <summary>
        /// Field is part of an index.
        /// </summary>
        public bool IsPartOfIndex { get; set; }

        /// <summary>
        /// Field is part of the primary key?
        /// </summary>
        public bool IsPartOfPrimaryKey { get; set; }

        /// <summary>
        /// The initial value.
        /// </summary>
        public string InitialValue { get; set; }

        /// <summary>
        /// Initial value attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        public string InitialValueAttribute { get; set; }

        /// <summary>
        /// Sql width.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// The label.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Label attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        public string LabelAttribute { get; set; }

        /// <summary>
        /// Column label.
        /// </summary>
        public string ColumnLabel { get; set; }

        /// <summary>
        /// ColumnLabel attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        public string ColumnLabelAttribute { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The help.
        /// </summary>
        public string Help { get; set; }

        /// <summary>
        /// Help attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        public string HelpAttribute { get; set; }

        /// <summary>
        /// The field triggers.
        /// </summary>
        public virtual IList<UoeDatabaseTrigger> Triggers { get; set; }
    }
}
