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

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// Represents a database trigger.
    /// </summary>
    public class UoeDatabaseTrigger {

        /// <summary>
        /// Type of event.
        /// </summary>
        public UoeDatabaseTriggerEvent Event { get; set; }

        /// <summary>
        /// The procedure linked ot this trigger.
        /// </summary>
        public string Procedure { get; set; }

        /// <summary>
        /// Is the trigger overridable?
        /// </summary>
        public bool Overridable { get; set; }

        /// <summary>
        /// Trigger procedure crc.
        /// </summary>
        public ushort Crc { get; set; }
    }
}
