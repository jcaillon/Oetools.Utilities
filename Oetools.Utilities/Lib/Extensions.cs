﻿#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (Extensions.cs) is part of csdeployer.
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Oetools.Utilities.Lib {

    /// <summary>
    /// This class regroups all the extension methods
    /// </summary>
    public static class Extensions {
        
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

        private static Dictionary<Type, List<Tuple<string, long>>> _enumTypeNameValueKeyPairs = new Dictionary<Type, List<Tuple<string, long>>>();

        public static void ForEach<T>(this Type curType, Action<string, long> actionForEachNameValue) {
            if (!curType.IsEnum)
                return;
            if (!_enumTypeNameValueKeyPairs.ContainsKey(curType)) {
                var list = new List<Tuple<string, long>>();
                foreach (var name in Enum.GetNames(curType)) {
                    var val = (T) Enum.Parse(curType, name);
                    list.Add(new Tuple<string, long>(name, Convert.ToInt64(val)));
                }
                _enumTypeNameValueKeyPairs.Add(curType, list);
            }
            foreach (var tuple in _enumTypeNameValueKeyPairs[curType]) actionForEachNameValue(tuple.Item1, tuple.Item2);
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
        ///     Allows to test if a string matches one of the listOfPattern (wildcards) in the list of patterns,
        ///     Ex : "file.xml".TestAgainstListOfPatterns("*.xls,*.com,*.xml") return true
        /// </summary>
        public static bool TestAgainstListOfPatterns(this string source, string listOfPattern) {
            return listOfPattern.Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList().Exists(s => source.RegexMatch(s.WildCardToRegex()));
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
        ///     Allows to tranform a matching string using * and ? (wildcards) into a valid regex expression
        ///     it escapes regex special char so it will work as you expect!
        ///     Ex: foo*.xls? will become ^foo.*\.xls.$
        ///     if the listOfPattern doesn't start with a * and doesn't end with a *, it adds both
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public static string WildCardToRegex(this string pattern) {
            if (string.IsNullOrEmpty(pattern))
                return ".*";
            var startStar = pattern[0].Equals('*');
            var endStar = pattern[pattern.Length - 1].Equals('*');
            return $"{(!startStar ? (endStar ? "^" : "") : "")}{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}{(!endStar ? (startStar ? "$" : "") : "")}";
        }

        /// <summary>
        ///     Allows to find a string with a regular expression, uses the IgnoreCase option by default, returns a match
        ///     collection,
        ///     to be used foreach (Match match in collection) { with match.Groups[1].Value being the first capture [2] etc...
        /// </summary>
        public static MatchCollection RegexFind(this string source, string regexString, RegexOptions options = RegexOptions.IgnoreCase) {
            var regex = new Regex(regexString, options);
            return regex.Matches(source);
        }

        /// <summary>
        ///     Allows to test a string with a regular expression, uses the IgnoreCase option by default
        ///     good website : https://regex101.com/
        /// </summary>
        public static bool RegexMatch(this string source, string regex, RegexOptions options = RegexOptions.IgnoreCase) {
            var reg = new Regex(regex, options);
            return reg.Match(source).Success;
        }

        /// <summary>
        ///     Allows to replace a string with a regular expression, uses the IgnoreCase option by default,
        ///     replacementStr can contains $1, $2...
        /// </summary>
        public static string RegexReplace(this string source, string regexString, string replacementStr, RegexOptions options = RegexOptions.IgnoreCase) {
            var regex = new Regex(regexString, options);
            return regex.Replace(source, replacementStr);
        }
        
        /// <summary>
        ///     Replaces " by "", replaces new lines by spaces and add extra " at the start and end of the string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string Quoter(this string text) {
            return $"\"{(text ?? "").Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "")}\"";
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

        public static bool ParseFtpAddress(this string ftpUri, out string ftpBaseUri, out string userName, out string passWord, out string host, out int port, out string relativePath) {
            var match = FtpUriRegex.Match(ftpUri.Replace("\\", "/"));
            if (match.Success) {
                ftpBaseUri = match.Groups[1].Value;
                relativePath = match.Groups[6].Value;
                if (match.Groups[4].Success) {
                    userName = match.Groups[2].Value;
                    passWord = match.Groups[3].Value.Substring(1);
                    host = match.Groups[4].Value.Substring(1);
                    int.TryParse(match.Groups[5].Value.Substring(1), out port);
                } else {
                    userName = null;
                    passWord = null;
                    host = match.Groups[2].Value;
                    int.TryParse(match.Groups[3].Value.Substring(1), out port);
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
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string CompactWhitespaces(this string s) {
            return new StringBuilder(s).CompactWhitespaces().ToString();
        }

        /// <summary>
        /// handle all whitespace chars not only spaces, trim both leading and trailing whitespaces, remove extra whitespaces,
        /// and all whitespaces are replaced to space char (so we have uniform space separator)
        /// </summary>
        /// <param name="sb"></param>
        public static StringBuilder CompactWhitespaces(this StringBuilder sb) {
            if (sb == null)
                return null;
            if (sb.Length == 0)
                return sb;

            // set [start] to first not-whitespace char or to sb.Length
            int start = 0;
            while (start < sb.Length) {
                if (char.IsWhiteSpace(sb[start]))
                    start++;
                else
                    break;
            }

            // if [sb] has only whitespaces, then return empty string
            if (start == sb.Length) {
                sb.Length = 0;
                return sb;
            }

            // set [end] to last not-whitespace char
            int end = sb.Length - 1;
            while (end >= 0) {
                if (char.IsWhiteSpace(sb[end]))
                    end--;
                else
                    break;
            }

            // compact string
            int dest = 0;
            bool previousIsWhitespace = false;
            for (int i = start; i <= end; i++) {
                if (char.IsWhiteSpace(sb[i])) {
                    if (!previousIsWhitespace) {
                        previousIsWhitespace = true;
                        sb[dest] = ' ';
                        dest++;
                    }
                } else {
                    previousIsWhitespace = false;
                    sb[dest] = sb[i];
                    dest++;
                }
            }

            sb.Length = dest;
            return sb;
        }
    }
}