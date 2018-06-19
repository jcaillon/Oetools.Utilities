#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProUtilities.cs) is part of Oetools.Utilities.
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using csdeployer.Lib;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Openedge {
    
    public static class ProUtilities {
        
        public static HashSet<string> GetProPathFromBaseDirectory(string baseDirectory) {
            
            var uniqueDirList = new HashSet<string>();

            foreach (var folder in Utils.EnumerateFolders(baseDirectory, "*", SearchOption.AllDirectories)) {
                if (!uniqueDirList.Contains(folder))
                    uniqueDirList.Add(folder);
            }

            return uniqueDirList;
        }
        
        public static HashSet<string> GetProPathFromIniFile(string iniFile, string sourceDirectory) {
            
            var uniqueDirList = new HashSet<string>();
            
            var propath = new IniReader(iniFile).GetValue("PROPATH", "");

            foreach (var path in propath
                .Split(',', '\n', ';')
                .Select(path => path.Trim())
                .Where(path => !string.IsNullOrEmpty(path))) {

                try {
                    var thisPath = path;
                
                    // replace environment variables
                    if (thisPath.Contains("%")) {
                        thisPath = Environment.ExpandEnvironmentVariables(thisPath);
                    }
                
                    // need to take into account relative paths
                    if (!Path.IsPathRooted(thisPath)) {
                        thisPath = Path.GetFullPath(Path.Combine(sourceDirectory, thisPath));
                    }

                    if (!Directory.Exists(thisPath) && !File.Exists(thisPath)) {
                        continue;
                    }

                    if (!uniqueDirList.Contains(thisPath)) {
                        uniqueDirList.Add(thisPath);
                    }
                } catch (Exception) {
                    // ignore bad directories
                }
            }

            return uniqueDirList;
        }
    }
}