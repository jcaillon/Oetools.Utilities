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
using System.Text;
using System.Text.RegularExpressions;
using csdeployer.Lib;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge {
    
    public static class ProUtilities {

        public static string GetDlcPath() {
            return Environment.GetEnvironmentVariable("dlc");
        }
        
        /// <summary>
        /// Returns the openedge version currently installed
        /// </summary>
        /// <remarks>https://knowledgebase.progress.com/articles/Article/P126</remarks>
        public static Version GetProVersionFromDlc(string dlcPath) {
            var versionFilePath = Path.Combine(dlcPath, "version");
            if (File.Exists(versionFilePath)) {
                var matches = new Regex(@"(\d+)\.(\d+)(?:\.(\d+)|([A-Za-z](\d+)))").Matches(File.ReadAllText(versionFilePath));
                if (matches.Count == 1) {
                    return new Version(int.Parse(matches[0].Groups[1].Value), int.Parse(matches[0].Groups[2].Value), int.Parse(matches[0].Groups[3].Success ? matches[0].Groups[3].Value : matches[0].Groups[5].Value));
                }
            }
            return new Version();
        }

        /// <summary>
        /// Returns wether or not the progress version accepts the -nosplash parameter
        /// </summary>
        /// <param name="proVersion"></param>
        /// <returns></returns>
        public static bool CanProVersionUseNoSplashParameter(Version proVersion) => proVersion.CompareTo(new Version(11, 6, 0)) >= 0;

        /// <summary>
        /// Returns the best suited pro executable full path from the dlc path
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="useCharacterModeOfProgress"></param>
        /// <returns></returns>
        public static string GetProExecutableFromDlc(string dlcPath, bool useCharacterModeOfProgress = false) {
            string outputPath;
            if (Utils.IsRuntimeWindowsPlatform) {
                outputPath = Path.Combine(dlcPath, "bin", useCharacterModeOfProgress ? "_progres.exe" : "prowin32.exe");
                if (!File.Exists(outputPath)) {
                    outputPath = Path.Combine(dlcPath, "bin", "prowin.exe");
                    if (!File.Exists(outputPath)) {
                        outputPath = Path.Combine(dlcPath, "bin", !useCharacterModeOfProgress ? "_progres.exe" : "prowin32.exe");
                    }
                }
            } else {
                outputPath = Path.Combine(dlcPath, "bin", "_progres");
            }
            return File.Exists(outputPath) ? outputPath : null;
        }

        /// <summary>
        /// Reads the propath from an ini file
        /// </summary>
        /// <param name="iniFile"></param>
        /// <param name="sourceDirectory"></param>
        /// <returns></returns>
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
        
        /// <summary>
        /// Reads a database connection string from a progress parameter file (takes comment into account)
        /// </summary>
        /// <param name="pfPath"></param>
        /// <returns></returns>
        public static string GetConnectionStringFromPfFile(string pfPath) {
            if (!File.Exists(pfPath))
                return string.Empty;

            var connectionString = new StringBuilder();
            Utils.ForEachLine(pfPath, new byte[0], (nb, line) => {
                if (!string.IsNullOrEmpty(line)) {
                    connectionString.Append(" ");
                    connectionString.Append(line);
                }
            });
            connectionString.Append(" ");
            return connectionString.CompactWhitespaces().ToString();
        }

        /// <summary>
        /// List all the prolib files in a directory
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <returns></returns>
        public static List<string> ListProlibFilesInDirectory(string baseDirectory) {
            return Utils.EnumerateFiles(baseDirectory, $"*{OeConstants.ExtProlibFile}", SearchOption.TopDirectoryOnly).ToList();
        }
    }
}