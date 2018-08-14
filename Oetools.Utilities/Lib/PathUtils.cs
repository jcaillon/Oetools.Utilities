#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (PathUtils.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Lib {
    /// <summary>
    ///     Class that exposes utility methods
    /// </summary>
    public static partial class Utils {
        
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
                throw new Exception($"Couldn\'t read the file {path.PrettyQuote()}", e);
            }
        }

        /// <summary>
        /// Delete a dir, recursively, doesn't throw an exception if it does not exists
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

        public static bool DeleteFileIfNeeded(string path) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }
            try {
                File.Delete(path);
            } catch (DirectoryNotFoundException) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Creates the directory if it doesn't exists, can apply attributes
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
        /// List all the folders in a folder
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="options"></param>
        /// <param name="excludePatterns">should be regex expressions</param>
        /// <param name="excludeHidden"></param>
        /// <returns></returns>
        public static IEnumerable<string> EnumerateAllFolders(string folderPath, SearchOption options = SearchOption.AllDirectories, List<string> excludePatterns = null, bool excludeHidden = false) {
            List<Regex> excludeRegexes = null;
            if (excludePatterns != null) {
                excludeRegexes = excludePatterns.Select(s => new Regex(s)).ToList();
            }
            var hiddenDirList = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var folderStack = new Stack<string>();
            folderStack.Push(folderPath);
            while (folderStack.Count > 0) {
                foreach (var dir in Directory.EnumerateDirectories(folderStack.Pop(), "*", SearchOption.TopDirectoryOnly)) {
                    if (hiddenDirList.Contains(dir)) {
                        continue;
                    }
                    if (excludeHidden && new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.Hidden)) {
                        hiddenDirList.Add(dir);
                        continue;
                    }
                    if (excludeRegexes != null && excludeRegexes.Any(r => r.IsMatch(dir))) {
                        continue;
                    }
                    if (options == SearchOption.AllDirectories) {
                        folderStack.Push(dir);
                    }
                    yield return dir;
                }                
            }
        }
        
        /// <summary>
        /// List all the files in a folder
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="options"></param>
        /// <param name="excludePatterns">should be regex expressions</param>
        /// <param name="excludeHiddenFolders"></param>
        /// <returns></returns>
        public static IEnumerable<string> EnumerateAllFiles(string folderPath, SearchOption options = SearchOption.AllDirectories, List<string> excludePatterns = null, bool excludeHiddenFolders = false) {
            List<Regex> excludeRegexes = null;
            if (excludePatterns != null) {
                excludeRegexes = excludePatterns.Select(s => new Regex(s)).ToList();
            }
            var folderStack = new Stack<string>();
            folderStack.Push(folderPath);
            while (folderStack.Count > 0) {
                var folder = folderStack.Pop();
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)) {
                    if (excludeRegexes != null && excludeRegexes.Any(r => r.IsMatch(file))) {
                        continue;
                    }
                    yield return file;
                }
                if (options == SearchOption.AllDirectories) {
                    foreach (var subfolder in EnumerateAllFolders(folder, SearchOption.TopDirectoryOnly, excludePatterns, excludeHiddenFolders)) {
                        folderStack.Push(subfolder);
                    }
                }
            }
        }

        /// <summary>
        /// List all the files in a given list of folders
        /// </summary>
        public static IEnumerable<string> EnumerateAllFiles(IEnumerable<string> folders, List<string> excludePatterns = null, bool excludeHiddenFolders = false) {
            foreach (var folder in folders) {
                foreach (var file in EnumerateAllFiles(folder, SearchOption.TopDirectoryOnly, excludePatterns, excludeHiddenFolders)) {
                    yield return file;
                }
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
            } else if (dataResources != null) {
                using (var reader = new StringReader(encoding.GetString(dataResources))) {
                    Action(reader);
                }
            }
        }

        /// <summary>
        /// Returns a temporary directory for this application
        /// </summary>
        /// <returns></returns>
        public static string GetTempDirectory(string subfolder = null) {
            var tmpDir = Path.Combine(Path.GetTempPath(), ".oe_tmp");
            if (!string.IsNullOrEmpty(subfolder)) {
                tmpDir = Path.Combine(tmpDir, subfolder);
            }
            CreateDirectoryIfNeeded(tmpDir);
            return tmpDir;
        }

        /// <summary>
        /// Returns the longest valid directory in a string
        /// </summary>
        /// <remarks>
        /// for instance
        /// - C:\windows\(any|thing)\(.*)
        /// will return
        /// - C;\windows
        /// </remarks>
        /// <param name="inputRegex"></param>
        /// <returns></returns>
        public static string GetLongestValidDirectory(string inputRegex) {
            var i = inputRegex.Length;
            string outputdirectory;
            do {
                outputdirectory = inputRegex.Substring(0, i);
                i--;
            } while (!Directory.Exists(outputdirectory) && i > 0);
            return outputdirectory;
        }
        
        /// <summary>
        /// - Test if the path wild card has correct matches &lt; &gt; place holders
        /// - Test if the path contains any invalid characters
        /// </summary>
        /// <remarks>We need this to know if the new Regex() will fail with this PathWildCard or not</remarks>
        /// <param name="pattern"></param>
        /// <exception cref="Exception"></exception>
        /// <returns></returns>
        public static void ValidatePathWildCard(string pattern) {
            if (string.IsNullOrEmpty(pattern)) {
                throw new Exception("The path is null or empty");
            }
            foreach (char c in Path.GetInvalidPathChars()) {
                if (c == '<' || c == '>' || c == '*' || c == '?') {
                    continue;
                }
                if (pattern.IndexOf(c) >= 0) {
                    throw new Exception($"Illegal character path {c} at column {pattern.IndexOf(c)}");
                }
            }
            pattern.ValidatePlaceHolders("<<", ">>");
        }

    }
}