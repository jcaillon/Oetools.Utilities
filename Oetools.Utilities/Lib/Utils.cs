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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Lib {
    /// <summary>
    ///     Class that exposes utility methods
    /// </summary>
    public static class Utils {
        
        /// <summary>
        /// Read all the text of a file in one go, same as File.ReadAllText expect it's truly a read only function
        /// </summary>
        public static string ReadAllText(string path, Encoding encoding = null) {
            try {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    using (var textReader = new StreamReader(fileStream, encoding ?? TextEncodingDetect.GetFileEncoding(path))) {
                        return textReader.ReadToEnd();
                    }
                }
            } catch (Exception e) {
                throw new Exception($"Couldn\'t read the file {path.Quoter()}", e);
            }
        }

        /// <summary>
        /// Delete a dir, recursively
        /// </summary>
        public static bool DeleteDirectoryIfExists(string path, bool recursive) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }
            try {
                Directory.Delete(path, recursive);
            } catch (DirectoryNotFoundException) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Creates the directory, can apply attributes
        /// </summary>
        public static bool CreateDirectoryIfNeeded(string path, FileAttributes attributes = FileAttributes.Directory) {
            if (Directory.Exists(path)) {
                return false;
            }
            var dirInfo = Directory.CreateDirectory(path);
            dirInfo.Attributes |= attributes;
            return true;
        }
        
                
        /// <summary>
        /// Returns the list of all the folders in a given folder
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <param name="excludeHiddenFolders"></param>
        /// <returns></returns>
        public static HashSet<string> ListAllFoldersFromBaseDirectory(string baseDirectory, bool excludeHiddenFolders = true) {
            
            var uniqueDirList = new HashSet<string>();

            foreach (var folder in EnumerateFolders(baseDirectory, "*", SearchOption.AllDirectories, excludeHiddenFolders)) {
                if (!uniqueDirList.Contains(folder))
                    uniqueDirList.Add(folder);
            }

            return uniqueDirList;
        }

        /// <summary>
        ///     Same as Directory.EnumerateDirectories but doesn't list hidden folders
        /// </summary>
        public static IEnumerable<string> EnumerateFolders(string folderPath, string pattern, SearchOption options, bool excludeHidden = false) {
            
            var hiddenDirList = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            
            foreach (var dir in Directory.EnumerateDirectories(folderPath, pattern, options)) {
                
                if (hiddenDirList.Contains(dir)) {
                    continue;
                }

                if (excludeHidden && new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.Hidden)) {
                    hiddenDirList.Add(dir);
                    continue;
                }

                yield return dir;
            }
        }

        /// <summary>
        /// Same as Directory.EnumerateFiles but is able to not list files in hidden folders
        /// </summary>
        public static IEnumerable<string> EnumerateFiles(string folderPath, string pattern, SearchOption options, bool excludeHidden = false) {
            foreach (var file in Directory.EnumerateFiles(folderPath, pattern, SearchOption.TopDirectoryOnly))
                yield return file;
            if (options == SearchOption.AllDirectories) {
                foreach (var file in EnumerateFiles(EnumerateFolders(folderPath, "*", SearchOption.AllDirectories, excludeHidden), pattern)) {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Same as Directory.EnumerateFiles
        /// </summary>
        public static IEnumerable<string> EnumerateFiles(IEnumerable<string> folders, string pattern) {
            foreach (var folder in folders) {
                foreach (var file in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly)) {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Returns true if the current execution is done on windows platform
        /// </summary>
        public static bool IsRuntimeWindowsPlatform {
            get {
#if WINDOWSONLYBUILD
                return true;
#else
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
            }
        }

        /// <summary>
        ///     Reads all the line of either the filePath (if the file exists) or from byte array dataResources,
        ///     Apply the action toApplyOnEachLine to each line
        ///     Uses encoding as the Encoding to read the file or convert the byte array to a string
        ///     Uses the char # as a comment in the file
        /// </summary>
        public static bool ForEachLine(string filePath, byte[] dataResources, Action<int, string> toApplyOnEachLine, Encoding encoding = null, Action<Exception> onException = null) {
            encoding = encoding ?? TextEncodingDetect.GetFileEncoding(filePath);
            var wentOk = true;
            try {
                SubForEachLine(filePath, dataResources, toApplyOnEachLine, encoding);
            } catch (Exception e) {
                wentOk = false;
                onException?.Invoke(e);

                // read default file, if it fails then we can't do much but to throw an exception anyway...
                if (dataResources != null) {
                    SubForEachLine(null, dataResources, toApplyOnEachLine, encoding);
                }
            }

            return wentOk;
        }

        private static void SubForEachLine(string filePath, byte[] dataResources, Action<int, string> toApplyOnEachLine, Encoding encoding) {
            // to apply on each line
            void Action(TextReader reader) {
                var i = 0;
                string line;
                while ((line = reader.ReadLine()) != null) {
                    if (line.Length > 0) {
                        var idx = line.IndexOf('#');
                        toApplyOnEachLine(i, idx > -1 ? line.Substring(0, idx) : (idx == 0 ? string.Empty : line));
                    }

                    i++;
                }
            }

            // either read from the file or from the byte array
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    using (var reader = new StreamReader(fileStream, encoding)) {
                        Action(reader);
                    }
                }
            } else {
                using (var reader = new StringReader(Encoding.Default.GetString(dataResources))) {
                    Action(reader);
                }
            }
        }
    }
}