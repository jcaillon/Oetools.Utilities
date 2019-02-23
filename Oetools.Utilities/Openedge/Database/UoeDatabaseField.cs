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
using Oetools.Utilities.Openedge.Database.Interfaces;

namespace Oetools.Utilities.Openedge.Database {

    /// <inheritdoc />
    public class UoeDatabaseField : IUoeDatabaseField {

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public UoeDatabaseDataType DataType { get; set; }

        /// <inheritdoc />
        public string Format { get; set; }

        /// <inheritdoc />
        public string FormatAttribute { get; set; }

        /// <inheritdoc />
        public int Order { get; set; }

        /// <inheritdoc />
        public int Position { get; set; }

        /// <inheritdoc />
        public bool Mandatory { get; set; }

        /// <inheritdoc />
        public bool CaseSensitive { get; set; }

        /// <inheritdoc />
        public int Extent { get; set; }

        /// <inheritdoc />
        public string InitialValue { get; set; }

        /// <inheritdoc />
        public string InitialValueAttribute { get; set; }

        /// <inheritdoc />
        public int Width { get; set; }

        /// <inheritdoc />
        public string Label { get; set; }

        /// <inheritdoc />
        public string LabelAttribute { get; set; }

        /// <inheritdoc />
        public string ColumnLabel { get; set; }

        /// <inheritdoc />
        public string ColumnLabelAttribute { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public string Help { get; set; }

        /// <inheritdoc />
        public string HelpAttribute { get; set; }

        /// <inheritdoc />
        public int Decimals { get; set; }

        /// <inheritdoc />
        public string ClobCharset { get; set; }

        /// <inheritdoc />
        public string ClobCollation { get; set; }

        /// <inheritdoc />
        public int ClobType { get; set; }

        /// <inheritdoc />
        public string LobSize { get; set; }

        /// <inheritdoc />
        public int LobBytes { get; set; }

        /// <inheritdoc />
        public string LobArea { get; set; }

        /// <inheritdoc />
        public virtual IList<IUoeDatabaseTrigger> Triggers { get; set; }
    }
}
