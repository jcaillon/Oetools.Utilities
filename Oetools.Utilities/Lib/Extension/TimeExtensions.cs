#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (TimeExtensions.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Lib.Extension {
    
    public static class TimeExtensions {
        
        /// <summary>
        /// Get the time elapsed in a human readable format
        /// </summary>
        public static string ConvertToHumanTime(this TimeSpan? tn) {
            if (tn == null) {
                return string.Empty;
            }
            var t = (TimeSpan) tn;
            if (t.Hours > 0)
                return $"{t.Hours:D2}h:{t.Minutes:D2}m:{t.Seconds:D2}s";
            if (t.Minutes > 0)
                return $"{t.Minutes:D2}m:{t.Seconds:D2}s";
            if (t.Seconds > 0)
                return $"{t.Seconds:D2}s";
            return $"{t.Milliseconds:D3}ms";
        }
    }
}