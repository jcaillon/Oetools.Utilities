#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (IUoeDatabaseSequence.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Openedge.Database.Interfaces {

    /// <summary>
    /// Represents a sequence.
    /// </summary>
    public interface IUoeDatabaseSequence {
        /// <summary>
        /// The sequence name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Will the value cycle when reaching the limit?
        /// </summary>
        bool CycleOnLimit { get; set; }

        /// <summary>
        /// The increment value.
        /// </summary>
        int Increment { get; set; }

        /// <summary>
        /// The initial value.
        /// </summary>
        int Initial { get; set; }

        /// <summary>
        /// The minimum sequence value.
        /// </summary>
        int? Min { get; set; }

        /// <summary>
        /// The maximum sequence value.
        /// </summary>
        int? Max { get; set; }
    }
}
