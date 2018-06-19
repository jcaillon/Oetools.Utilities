#region header

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

#endregion

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
        
        #region File manipulation wrappers

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
        public static bool DeleteDirectory(string path, bool recursive) {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return true;
            Directory.Delete(path, recursive);
            return true;
        }

        /// <summary>
        ///     Creates the directory, can apply attributes
        /// </summary>
        public static bool CreateDirectory(string path, FileAttributes attributes = FileAttributes.Directory) {
            try {
                if (Directory.Exists(path))
                    return true;
                var dirInfo = Directory.CreateDirectory(path);
                dirInfo.Attributes |= attributes;
            } catch (Exception e) {
                throw new Exception("Couldn't create the folder " + path.Quoter(), e);
            }

            return true;
        }

        /// <summary>
        ///     Same as Directory.EnumerateDirectories but doesn't list hidden folders
        /// </summary>
        public static IEnumerable<string> EnumerateFolders(string folderPath, string pattern, SearchOption options) {
            var hiddenDirList = new List<string>();
            foreach (var dir in Directory.EnumerateDirectories(folderPath, pattern, options)) {
                if (hiddenDirList.Exists(d => dir.StartsWith(d, StringComparison.CurrentCultureIgnoreCase))) {
                    continue;
                }
                if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.Hidden)) {
                    hiddenDirList.Add(dir);
                    continue;
                }
                yield return dir;
            }
        }

        /// <summary>
        ///     Same as Directory.EnumerateFiles but doesn't list files in hidden folders
        /// </summary>
        public static IEnumerable<string> EnumerateFiles(string folderPath, string pattern, SearchOption options) {
            foreach (var file in Directory.EnumerateFiles(folderPath, pattern, SearchOption.TopDirectoryOnly))
                yield return file;
            if (options == SearchOption.AllDirectories) {
                foreach (var folder in EnumerateFolders(folderPath, "*", SearchOption.AllDirectories)) {
                    foreach (var file in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly)) {
                        yield return file;
                    }
                }
            }
        }

        #endregion
        
        public static string GetConnectionStringFromPfFile(string pfPath) {
            if (!File.Exists(pfPath))
                return string.Empty;

            var connectionString = new StringBuilder();
            ForEachLine(pfPath, new byte[0], (nb, line) => {
                if (!string.IsNullOrEmpty(line)) {
                    connectionString.Append(" ");
                    connectionString.Append(line);
                }
            });
            connectionString.Append(" ");
            return connectionString.CompactWhitespaces().ToString();
        }

        public static bool IsRuntimeWindowsPlatform {
            get {
#if WINDOWSONLYBUILD
                return true;
#else
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
            }
        }
        

        #region Read a configuration file

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

        #endregion
    }
}