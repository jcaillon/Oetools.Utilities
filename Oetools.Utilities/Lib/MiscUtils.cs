// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (Utils.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

using System.Runtime.InteropServices;

namespace Oetools.Utilities.Lib {
    /// <summary>
    ///     Class that exposes utility methods
    /// </summary>
    public static partial class Utils {

#if !WINDOWSONLYBUILD
        private static bool? _isRuntimeWindowsPlatform;
#endif
        
        /// <summary>
        /// Returns true if the current execution is done on windows platform
        /// </summary>
        public static bool IsRuntimeWindowsPlatform {
            get {
#if WINDOWSONLYBUILD
                return true;
#else
                return (_isRuntimeWindowsPlatform ?? (_isRuntimeWindowsPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows))).Value;
#endif
            }
        }
        
    }
}