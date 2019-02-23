#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (IUoeDatabaseField.cs) is part of Oetools.Utilities.
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
    /// Represents an openedge database field.
    /// </summary>
    public interface IUoeDatabaseField {

        /// <summary>
        /// Field name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Field type.
        /// </summary>
        UoeDatabaseDataType DataType { get; set; }

        /// <summary>
        /// Format.
        /// </summary>
        string Format { get; set; }

        /// <summary>
        /// Format attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        string FormatAttribute { get; set; }

        /// <summary>
        /// Order (increment 10 by 10).
        /// </summary>
        int Order { get; set; }

        /// <summary>
        /// Position (stars at 2).
        /// </summary>
        int Position { get; set; }

        /// <summary>
        /// Is field mandatory?
        /// </summary>
        bool Mandatory { get; set; }

        /// <summary>
        /// Is field case sensitive?
        /// </summary>
        bool CaseSensitive { get; set; }

        /// <summary>
        /// Extent (is the field an array, it is the size of this array).
        /// </summary>
        int Extent { get; set; }

        /// <summary>
        /// The initial value.
        /// </summary>
        string InitialValue { get; set; }

        /// <summary>
        /// Initial value attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        string InitialValueAttribute { get; set; }

        /// <summary>
        /// Sql width.
        /// </summary>
        int Width { get; set; }

        /// <summary>
        /// The label.
        /// </summary>
        string Label { get; set; }

        /// <summary>
        /// Label attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        string LabelAttribute { get; set; }

        /// <summary>
        /// Column label.
        /// </summary>
        string ColumnLabel { get; set; }

        /// <summary>
        /// ColumnLabel attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        string ColumnLabelAttribute { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// The help.
        /// </summary>
        string Help { get; set; }

        /// <summary>
        /// Help attribute.
        /// </summary>
        /// <inheritdoc cref="UoeDatabaseTable.LabelAttribute"/>
        string HelpAttribute { get; set; }

        /// <summary>
        /// Indicates the number of decimal places, after the decimal point, that are stored for a buffer-field object that corresponds to a DECIMAL field. Valid values are zero (0) to ten (10).
        /// </summary>
        int Decimals { get; set; }

        /// <summary>
        /// The database codepage.
        /// </summary>
        string ClobCharset { get; set; }

        /// <summary>
        /// The database collation.
        /// </summary>
        string ClobCollation { get; set; }

        /// <summary>
        /// CLOB-TYPE in .df files:
        /// CLOB-TYPE 1 means code page and collation are the same as db
        /// CLOB-TYPE 2 means User specified code page and collation and is different from the database.
        /// </summary>
        int ClobType { get; set; }

        /// <summary>
        /// Size of a large binary object. Format: 103M.
        /// </summary>
        string LobSize { get; set; }

        /// <summary>
        /// Size of a large binary object in bytes.
        /// </summary>
        int LobBytes { get; set; }

        /// <summary>
        /// The area in which the LOB is stored.
        /// </summary>
        string LobArea { get; set; }

        /// <summary>
        /// The field triggers.
        /// </summary>
        IList<IUoeDatabaseTrigger> Triggers { get; set; }
    }
}
