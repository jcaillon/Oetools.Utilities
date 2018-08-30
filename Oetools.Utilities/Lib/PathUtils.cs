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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Lib {
    /// <summary>
    ///     Class that exposes utility methods
    /// </summary>
    public static partial class Utils {

        public static FileList<T> ToFileList<T>(this IEnumerable<T> list) where T : IFileListItem {
            var output = new FileList<T>();
            output.TryAddRange(list);
            return output;
        }
        
        /// <summary>
        /// Returns true if two path are equals
        /// </summary>
        /// <param name="path"></param>
        /// <param name="path2"></param>
        /// <returns></returns>
        public static bool PathEquals(this string path, string path2) {
            if (path == null || path2 == null) {
                return path == null && path2 == null;
            }
            return path.Equals(path2, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Test if two paths are on the same drive (for instance D:\folder and D:\file.ext are on the same drive D:),
        /// if we have no way of knowing (for instance, if 
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <returns></returns>
        public static bool ArePathOnSameDrive(string path1, string path2) {
            if (!IsRuntimeWindowsPlatform) {
                return true;
            }
            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2)) {
                return true;
            }
            if (path1.Length < 2 || path1[1] != Path.VolumeSeparatorChar) {
                return true;
            }
            if (path2.Length < 2 || path2[1] != Path.VolumeSeparatorChar) {
                return true;
            }
            return path1[0] == path2[0];
        }
        
        /// <summary>
        /// Make sure to trim the ending "\" or "/"
        /// </summary>
        public static string TrimEndDirectorySeparator(this string path) {
            if (string.IsNullOrEmpty(path)) {
                return path;
            }
            return path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.AltDirectorySeparatorChar ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
        }
        
        /// <summary>
        /// Make sure to trim the starting "\" or "/"
        /// </summary>
        public static string TrimStartDirectorySeparator(this string path) {
            if (string.IsNullOrEmpty(path)) {
                return path;
            }
            return path[0] == Path.DirectorySeparatorChar || path[0] == Path.AltDirectorySeparatorChar ? path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : path;
        }

        /// <summary>
        /// Transform a relative to an absolute path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="currentDirectory"></param>
        /// <returns></returns>
        public static string MakePathAbsolute(this string path, string currentDirectory = null) {
            if (Path.IsPathRooted(path)) {
                return path;
            }
            return Path.Combine(currentDirectory ?? Directory.GetCurrentDirectory(), path);
        }
        
        /// <summary>
        ///     Replaces all invalid characters found in the provided name
        /// </summary>
        /// <param name="fileName">A file name without directory information</param>
        /// <param name="replacementChar"></param>
        /// <returns></returns>
        public static string ToValidLocalFileName(this string fileName, char replacementChar = '_') {
            return ReplaceAllChars(fileName, Path.GetInvalidFileNameChars(), replacementChar);
        }
        
        private static string ReplaceAllChars(string str, char[] oldChars, char newChar) {
            var sb = new StringBuilder(str);
            foreach (var c in oldChars)
                sb.Replace(c, newChar);
            return sb.ToString();
        }
        
        /// <summary>
        /// Transforms an absolute path into a relative one
        /// </summary>
        /// <param name="absolute"></param>
        /// <param name="pathToDelete"></param>
        /// <returns></returns>
        public static string FromAbsolutePathToRelativePath(this string absolute, string pathToDelete) {
            if (string.IsNullOrEmpty(absolute) || string.IsNullOrEmpty(pathToDelete)) {
                return absolute;
            }
            var relative = absolute.Replace(pathToDelete, "");
            return relative.Length == absolute.Length ? absolute : relative.TrimStartDirectorySeparator();
        }
        
        /// <summary>
        /// Gets a messy path (can be valid or not) and returns a cleaned path, trimming ending dir sep char
        /// TODO : also replace stuff like /./ or /../
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ToCleanPath(this string path) {
            if (string.IsNullOrEmpty(path)) {
                return path;
            }
            string newPath = null;
            bool isWindows = IsRuntimeWindowsPlatform;
            bool startDoubleSlash = false;
            if (isWindows) {
                if (path.IndexOf('/') >= 0) {
                    newPath = path.Replace('/', '\\');
                }
                startDoubleSlash = path.Length >= 2 && (newPath ?? path)[0] == '\\' && (newPath ?? path)[1] == '\\';
                if ((newPath ?? path).Length > 0 && (newPath ?? path)[0] == '\\' && !startDoubleSlash) {
                    // replace / by C:\
                    newPath = $"{Path.GetFullPath(@"/")}{(newPath ?? path).Substring(1)}";
                }
            } else {
                if (path.IndexOf('\\') >= 0) {
                    newPath = path.Replace('\\', '/');
                }
            }
            // clean consecutive /
            int idx;
            do {
                idx = (newPath ?? path).IndexOf($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                if (idx >= 0) {
                    newPath = (newPath ?? path).Replace($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}");
                }
            } while (idx >= 0);
            if (!string.IsNullOrEmpty(newPath) && startDoubleSlash) {
                newPath = $"{Path.DirectorySeparatorChar}{newPath}";
            }
            return (newPath ?? path).TrimEndDirectorySeparator();
        }
        
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
        /// <param name="cancelSource"></param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        public static IEnumerable<string> EnumerateAllFolders(string folderPath, SearchOption options = SearchOption.AllDirectories, List<string> excludePatterns = null, bool excludeHidden = false, CancellationTokenSource cancelSource = null) {
            List<Regex> excludeRegexes = null;
            if (excludePatterns != null) {
                excludeRegexes = excludePatterns.Select(s => new Regex(s)).ToList();
            }
            var hiddenDirList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folderStack = new Stack<string>();
            folderStack.Push(folderPath);
            while (folderStack.Count > 0) {
                cancelSource?.Token.ThrowIfCancellationRequested();
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
        /// <param name="cancelSource"></param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        public static IEnumerable<string> EnumerateAllFiles(string folderPath, SearchOption options = SearchOption.AllDirectories, List<string> excludePatterns = null, bool excludeHiddenFolders = false, CancellationTokenSource cancelSource = null) {
            List<Regex> excludeRegexes = null;
            if (excludePatterns != null) {
                excludeRegexes = excludePatterns.Select(s => new Regex(s)).ToList();
            }
            var folderStack = new Stack<string>();
            folderStack.Push(folderPath);
            while (folderStack.Count > 0) {
                cancelSource?.Token.ThrowIfCancellationRequested();
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
        /// Returns a random file name (can be used for folder aswell) 
        /// </summary>
        /// <returns></returns>
        public static string GetRandomName() {
            return $"{DateTime.Now:fff}{Path.GetRandomFileName()}";
        }

        /// <summary>
        /// Returns the longest valid (and existing) directory in a string, return null if nothing matches
        /// </summary>
        /// <remarks>
        /// for instance
        /// - C:\windows\(any|thing)\(.*)
        /// will return
        /// - C:\windows
        /// </remarks>
        /// <param name="inputWildCardPath"></param>
        /// <returns></returns>
        public static string GetLongestValidDirectory(string inputWildCardPath) {
            inputWildCardPath = inputWildCardPath.ToCleanPath();
            var i = inputWildCardPath.Length;
            string outputdirectory;
            do {
                outputdirectory = inputWildCardPath.Substring(0, i);
                i--;
            } while (!Directory.Exists(outputdirectory) && i > 0);
            return i == 0 ? null : outputdirectory;
        }
        
        /// <summary>
        ///     Allows to test if a string matches one of the listOfPattern (wildcards) in the list of patterns,
        ///     Ex : "file.xml".TestAgainstListOfPatterns("*.xls;*.com;*.xml") return true
        ///     Ex : "path/file.xml".TestAgainstListOfPatterns("*.xls;*.com;*.xml") return false!
        /// </summary>
        public static bool TestAgainstListOfPatterns(this string source, string listOfPattern) {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(listOfPattern)) {
                return false;
            }
            return listOfPattern.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList().Exists(s => new Regex(s.PathWildCardToRegex()).IsMatch(source));
        }

        /// <summary>
        ///     Allows to test if a string matches one of the listOfPattern (wildcards) in the list of patterns,
        ///     Ex : "file.xml".TestAgainstListOfPatterns("*.xls;*.com;*.xml") return true
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public static bool TestFileNameAgainstListOfPatterns(this string filePath, string listOfPattern) {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(listOfPattern)) {
                return false;
            }
            return listOfPattern.Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList().Exists(s => new Regex(s.PathWildCardToRegex()).IsMatch(Path.GetFileName(filePath)));
        }

        /// <summary>
        ///     Allows to tranform a matching string using **, * and ? (wildcards) into a valid regex expression
        ///     it escapes regex special char so it will work as you expect!
        ///     Ex: foo*.xls? will become ^foo.*\.xls.$
        ///     - ** matches any char any nb of time (greedy match! allows to do stuff like C:\((**))((*)).txt)
        ///     - * matches only non path separators any time
        ///     - ? matches non path separators 1 time
        ///     - (( will be transformed into open capturing parenthesis
        ///     - )) will be transformed into close capturing parenthesis
        ///     - || will be transformed into |
        /// </summary>
        /// <param name="pattern"></param>
        /// <remarks>
        /// validate the pattern first with <see cref="Utils.ValidatePathWildCard"/> to make sure the (( and )) are legit
        /// </remarks>
        /// <returns></returns>
        public static string PathWildCardToRegex(this string pattern) {
            if (string.IsNullOrEmpty(pattern)) {
                return null;
            }
            pattern = Regex.Escape(pattern.Replace("\\", "/"))
                .Replace(@"\(\(", @"(")
                .Replace(@"\)\)", @")")
                .Replace(@"\|\|", @"|")
                .Replace(@"/", @"[\\/]")
                .Replace(@"\*\*", ".*?")
                .Replace(@"\*", @"[^\\/]*")
                .Replace(@"\?", @"[^\\/]")
                ;
            return $"^{pattern}$";
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
                if (c == '*' || c == '?' || c == '|') {
                    continue;
                }
                if (pattern.IndexOf(c) >= 0) {
                    throw new Exception($"Illegal character path {c} at column {pattern.IndexOf(c)}");
                }
            }
            pattern.ValidatePlaceHolders("((", "))");
        }

        /// <summary>
        /// Equivalent to <see cref="Path.IsPathRooted"/> but throws no exceptions
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsPathRooted(string path) {
            try {
                return Path.IsPathRooted(path);
            } catch (Exception) {
                return false;
            }
        }

        /// <summary>
        /// Returns the Md5 print of a file as a string
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string GetMd5FromFilePath(string filePath) {
            using (var md5 = MD5.Create()) {
                using (var stream = File.OpenRead(filePath)) {
                    StringBuilder sBuilder = new StringBuilder();
                    foreach (var b in md5.ComputeHash(stream)) {
                        sBuilder.Append(b.ToString("x2"));
                    }
                    // Return the hexadecimal string
                    return sBuilder.ToString();
                }
            }
        }
        
    }
}