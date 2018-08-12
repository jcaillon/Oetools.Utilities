// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (CssResources.cs) is part of Oetools.HtmlExport.
// 
// Oetools.HtmlExport is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.HtmlExport is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.HtmlExport. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

using System;
using System.Collections.Generic;
using System.Text;

namespace Oetools.Utilities.Resources {
    
    internal static class OpenedgeResources {
        
        private static byte[] GetOpenedgeFromResources(string fileName) {
            return Resources.GetBytesFromResource($"{nameof(Oetools)}.{nameof(Utilities)}.{nameof(Resources)}.Openedge.{fileName}");
        }

        private static Dictionary<string, string> _openedgeAsString = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        
        /// <summary>
        /// Returns the openedge program/file as a string from the resources
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <remarks>we cache the files here, but it might not be necessary?</remarks>
        public static string GetOpenedgeAsStringFromResources(string fileName) {
            if (!_openedgeAsString.ContainsKey(fileName)) {
                _openedgeAsString.Add(fileName, Encoding.Default.GetString(GetOpenedgeFromResources(fileName)));
            }
            return _openedgeAsString[fileName];
        }
        
    }
}