// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (StringExtensions.cs) is part of Oetools.Utilities.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Oetools.Utilities.Test")]

namespace Oetools.Utilities.Lib.Extension {
    
    public static class StringExtensions {

        /// <summary>
        /// Remove quotes from a string
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string StripQuotes(this string source) {
            if (string.IsNullOrEmpty(source)) {
                return source;
            }
            return source.Length > 1 && source[0] == source[source.Length - 1] && source[0] == '"' ? (source.Length - 2 > 0 ? source.Substring(1, source.Length - 2) : "") : source;
        }
        
        /// <summary>
        ///     Converts a string to an object of the given type
        /// </summary>
        public static object ConvertFromStr(this string value, Type destType) {
            try {
                if (destType == typeof(string))
                    return value;
                return TypeDescriptor.GetConverter(destType).ConvertFromInvariantString(value);
            } catch (Exception) {
                return destType.IsValueType ? Activator.CreateInstance(destType) : null;
            }
        }

        /// <summary>
        ///     Equivalent to Equals but case insensitive
        /// </summary>
        /// <param name="s"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        public static bool EqualsCi(this string s, string comp) {
            //string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);
            return s.Equals(comp, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        ///     case insensitive contains
        /// </summary>
        /// <param name="source"></param>
        /// <param name="toCheck"></param>
        /// <returns></returns>
        public static bool ContainsFast(this string source, string toCheck) {
            return source.IndexOf(toCheck, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        /// <summary>
        ///     Allows to test if a string matches one of the listOfPattern (wildcards) in the list of patterns,
        ///     Ex : "file.xml".TestAgainstListOfPatterns("*.xls,*.com,*.xml") return true
        ///     Ex : "path/file.xml".TestAgainstListOfPatterns("*.xls,*.com,*.xml") return false!
        /// </summary>
        public static bool TestAgainstListOfPatterns(this string source, string listOfPattern) {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(listOfPattern)) {
                return false;
            }
            return listOfPattern.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList().Exists(s => new Regex(s.PathWildCardToRegex()).IsMatch(source));
        }

        /// <summary>
        ///     Allows to test if a string matches one of the listOfPattern (wildcards) in the list of patterns,
        ///     Ex : "file.xml".TestAgainstListOfPatterns("*.xls,*.com,*.xml") return true
        /// </summary>
        public static bool TestFileNameAgainstListOfPatterns(this string filePath, string listOfPattern) {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(listOfPattern)) {
                return false;
            }
            return listOfPattern.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList().Exists(s => new Regex(s.PathWildCardToRegex()).IsMatch(Path.GetFileName(filePath)));
        }

        /// <summary>
        ///     Allows to tranform a matching string using **, * and ? (wildcards) into a valid regex expression
        ///     it escapes regex special char so it will work as you expect!
        ///     Ex: foo*.xls? will become ^foo.*\.xls.$
        ///     - ** matches any char any nb of time (greedy match!)
        ///     - * matches only non path separators any time
        ///     - ? matches non path separators 1 time
        ///     - &lt; will be transformed into open capturing parenthesis
        ///     - &gt; will be transformed into close capturing parenthesis
        /// </summary>
        /// <param name="pattern"></param>
        /// <remarks>validate the pattern first with <see cref="IsPlaceHolderPathValid"/></remarks>
        /// <returns></returns>
        public static string PathWildCardToRegex(this string pattern) {
            if (string.IsNullOrEmpty(pattern)) {
                return null;
            }
            pattern = Regex.Escape(pattern.Replace("\\", "/"))
                .Replace(@"<", @"(")
                .Replace(@">", @")")
                .Replace(@"/", @"[\\/]")
                .Replace(@"\*\*", ".*?")
                .Replace(@"\*", @"[^\\/]*")
                .Replace(@"\?", @"[^\\/]")
                ;
            return $"^{pattern}$";
        }

        /// <summary>
        /// Checks if a string has correct place horders, return false if they are opened and not closed
        /// i.e. : "^zf^ez$f$" return true with tags ^ start $ end and depth 2
        /// </summary>
        /// <param name="source"></param>
        /// <param name="openPo"></param>
        /// <param name="closePo"></param>
        /// <param name="maxDepth"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static bool HasValidPlaceHolders(this string source, string openPo, string closePo, int maxDepth = 0, StringComparison comparison = StringComparison.Ordinal) {
            int stack = 0;
            int idx = 0;
            do {
                var idxStart = source.IndexOf(openPo, idx, comparison);
                var idxEnd = source.IndexOf(closePo, idx, comparison);
                if (idxStart >= 0 && (idxEnd < 0 || idxStart < idxEnd)) {
                    idx = idxStart;
                    stack++;
                } else {
                    if (idxEnd >= 0) {
                        stack--;
                    }
                    idx = idxEnd;
                }
                if (stack < 0) {
                    return false;
                }
                if (maxDepth > 0 && stack > maxDepth) {
                    return false;
                }
                idx++;
            } while (idx > 0 && idx <= source.Length - 1);

            return stack == 0;
        }

        /// <summary>
        /// - Test if the path wild card has correct matches &lt; &gt; place holders
        /// - Test if the path contains any invalid characters
        /// </summary>
        /// <remarks>We need this to know if the new Regex() will fail with this PathWildCard or not</remarks>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static bool IsPlaceHolderPathValid(this string pattern) {
            if (string.IsNullOrEmpty(pattern)) {
                return false;
            }
            foreach (char c in Path.GetInvalidPathChars()) {
                if (c == '<' || c == '>') {
                    continue;
                }
                if (pattern.IndexOf(c) >= 0) {
                    return false;
                }
            }
            return pattern.HasValidPlaceHolders("<", ">");
        }
        
        /// <summary>
        /// Replace the place holders in a string by a value
        /// </summary>
        /// <remarks>doesn't do any checks, you have to validate that the source is correct first using <see cref="HasValidPlaceHolders"/></remarks>
        /// <remarks>Also make sure that replacementFunction will not return a string containing the opening or closing!</remarks>
        /// <param name="replacementString"></param>
        /// <param name="replacementFunction"></param>
        /// <param name="openPo"></param>
        /// <param name="closePo"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static string ReplacePlaceHolders(this string replacementString, Func<string, string> replacementFunction, string openPo = "<", string closePo = ">", StringComparison comparison = StringComparison.Ordinal) {
            int idxStart;
            int idx = 0;
            int loop = 0;
            do {
                loop++;
                if (loop > 100) {
                    throw new Exception("Trapped in infinite loop make sure the replacement function doesn't return a placeholder token");
                }
                idxStart = replacementString.IndexOf(openPo, idx, comparison);
                if (idxStart >= 0) {
                    var idxEnd = replacementString.IndexOf(closePo, idxStart + openPo.Length, comparison);
                    var nextIdxStart = replacementString.IndexOf(openPo, idxStart + openPo.Length, comparison);
                    if (nextIdxStart < 0 || idxEnd < nextIdxStart) {
                        var variableName = replacementString.Substring(idxStart + openPo.Length, idxEnd - (idxStart + openPo.Length));
                    replacementString = replacementString.Remove(idxStart, idxEnd + closePo.Length - idxStart).Insert(idxStart, replacementFunction(variableName));
                        idx = 0;
                    } else {
                        idx = idxStart + openPo.Length;
                    }
                }
            } while (idxStart >= 0);
            return replacementString;
        }

        /// <summary>
        ///     Replaces " by "", replaces new lines by spaces and add extra " at the start and end of the string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Quoter(this string text) {
            if (string.IsNullOrEmpty(text)) {
                return "\"null\"";
            }
            return $"\"{text.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "")}\"";
        }

        /// <summary>
        ///     Format a text to use as a single line CHARACTER string encapsulated in double quotes "
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static string ProQuoter(this string text) {
            return $"\"{text?.Replace("\"", "~\"").Replace("\\", "~\\").Replace("/", "~/").Replace("*", "~*").Replace("\n", "~n").Replace("\r", "~r") ?? ""}\"";
        }

        /// <summary>
        ///     Uses ProQuoter then make sure to escape every ~ with a double ~~
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string PreProcQuoter(this string text) {
            return text.ProQuoter().Replace("~", "~~");
        }

        /// <summary>
        ///     Make sure the directory finished with "\"
        /// </summary>
        public static string CorrectDirPath(this string path) {
            return $@"{path.TrimEnd('\\')}\";
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
        ///     Same as ToList but returns an empty list on Null instead of an exception
        /// </summary>
        public static List<T> ToNonNullList<T>(this IEnumerable<T> obj) {
            return obj == null ? new List<T>() : obj.ToList();
        }

        private static Regex _regex;

        private static Regex FtpUriRegex => _regex ?? (_regex = new Regex(@"^(ftps?:\/\/([^:\/@]*)?(:[^:\/@]*)?(@[^:\/@]*)?(:[^:\/@]*)?)(\/.*)$", RegexOptions.Compiled));
        
        /// <summary>
        ///     Returns true if the ftp uri is valid
        /// </summary>
        public static bool IsValidFtpAddress(this string ftpUri) {
            return FtpUriRegex.Match(ftpUri.Replace("\\", "/")).Success;
        }

        /// <summary>
        /// Parses the given FTP URI into strings
        /// </summary>
        /// <param name="ftpUri"></param>
        /// <param name="ftpBaseUri"></param>
        /// <param name="userName"></param>
        /// <param name="passWord"></param>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public static bool ParseFtpAddress(this string ftpUri, out string ftpBaseUri, out string userName, out string passWord, out string host, out int port, out string relativePath) {
            var match = FtpUriRegex.Match(ftpUri.Replace("\\", "/"));
            if (match.Success) {
                ftpBaseUri = match.Groups[1].Value;
                relativePath = match.Groups[6].Value;
                port = 0;
                if (match.Groups[4].Success) {
                    userName = match.Groups[2].Value;
                    if (match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value)) {
                        passWord = match.Groups[3].Value.Substring(1);
                    } else {
                        passWord = null;
                    }
                    host = match.Groups[4].Value.Substring(1);
                    if (!string.IsNullOrWhiteSpace(match.Groups[5].Value)) {
                        int.TryParse(match.Groups[5].Value.Substring(1), out port);
                    }
                } else {
                    userName = null;
                    passWord = null;
                    host = match.Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(match.Groups[3].Value)) {
                        int.TryParse(match.Groups[3].Value.Substring(1), out port);
                    }
                }
            } else {
                ftpBaseUri = null;
                relativePath = null;
                userName = null;
                passWord = null;
                host = null;
                port = 0;
            }
            return match.Success;
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
        /// handle all whitespace chars not only spaces, trim both leading and trailing whitespaces, remove extra whitespaces,
        /// and all whitespaces are replaced to space char (so we have uniform space separator)
        /// Will not compact whitespaces inside quotes or double quotes
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string CompactWhitespaces(this string s) {
            return new StringBuilder(s).CompactWhitespaces().ToString();
        }
    }
}