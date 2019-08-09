#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeTrigger.cs) is part of Oetools.Utilities.
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

using Oetools.Utilities.Openedge.Database.Interfaces;

namespace Oetools.Utilities.Openedge.Database {

    /// <inheritdoc />
    public class UoeDatabaseTrigger : IUoeDatabaseTrigger {

        /// <inheritdoc />
        public UoeDatabaseTriggerEvent Event { get; set; }

        /// <inheritdoc />
        public string Procedure { get; set; }

        /// <inheritdoc />
        public bool Overridable { get; set; }

        /// <inheritdoc />
        public ushort Crc { get; set; }
    }
}
