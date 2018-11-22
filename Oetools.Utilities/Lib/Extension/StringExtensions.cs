#region header
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
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Oetools.Utilities.Test")]

namespace Oetools.Utilities.Lib.Extension {
    
    public static class StringExtensions {

        /// <summary>
        /// Returns either the original string or a default if the original string is null or empty (whitespaces only)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static string TakeDefaultIfNeeded(this string source, string defaultValue) {
            return string.IsNullOrWhiteSpace(source) ? defaultValue : source;
        }
        
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
        ///     Equivalent to Equals but case insensitive, also handles null case
        /// </summary>
        /// <param name="s"></param>
        /// <param name="comp"></param>
        /// <returns></returns>
        public static bool EqualsCi(this string s, string comp) {
            if (s == null || comp == null) {
                return s == null && comp == null;
            }
            return s.Equals(comp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     case insensitive contains
        /// </summary>
        /// <param name="source"></param>
        /// <param name="toCheck"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static bool ContainsCi(this string source, string toCheck, StringComparison comparison = StringComparison.Ordinal) {
            return source.IndexOf(toCheck, comparison) >= 0;
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
        public static void ValidatePlaceHolders(this string source, string openPo = "{{", string closePo = "}}", int maxDepth = 0, StringComparison comparison = StringComparison.Ordinal) {
            source.ReplacePlaceHolders(null, openPo, closePo, maxDepth, comparison);
        }
        
        /// <summary>
        /// Replace the place holders in a string by a value
        /// </summary>
        /// <remarks>will throw errors, you have to validate that the source is correct first using <see cref="ValidatePlaceHolders"/></remarks>
        /// <remarks>will throw errors if your replacement string contains an open or clo</remarks>
        /// <param name="source"></param>
        /// <param name="replacementFunction"></param>
        /// <param name="openPo"></param>
        /// <param name="closePo"></param>
        /// <param name="maxDepth"></param>
        /// <param name="comparison"></param>
        /// <exception cref="Exception"></exception>
        /// <returns></returns>
        public static string ReplacePlaceHolders(this string source, Func<string, string> replacementFunction, string openPo = "{{", string closePo = "}}", int maxDepth = 0, StringComparison comparison = StringComparison.Ordinal) {
            var startPosStack = new Stack<int>();
            var osb = replacementFunction == null ? source : $"{source}";
            int idx = 0;
            do {
                var idxStart = osb.IndexOf(openPo, idx, comparison);
                var idxEnd = osb.IndexOf(closePo, idx, comparison);
                if (idxStart >= 0 && (idxEnd < 0 || idxStart < idxEnd)) {
                    idx = idxStart;
                    startPosStack.Push(idxStart);
                } else {
                    idx = idxEnd;
                    if (idxEnd >= 0) {
                        if (startPosStack.Count == 0) {
                            throw new Exception($"Invalid symbol {closePo} found at column {idx} (no corresponding {openPo}).");
                        }
                        var lastStartPos = startPosStack.Pop();
                        if (replacementFunction != null) {
                            // we need to replace this closed place holder
                            var variableName = osb.Substring(lastStartPos + openPo.Length, idxEnd - (lastStartPos + openPo.Length));
                            var variableValue = replacementFunction(variableName);
                            if (variableValue != null) {
                                if (variableValue.IndexOf(openPo, 0, comparison) >= 0) {
                                    throw new Exception($"The place holder value can't contain {openPo}.");
                                }
                                osb = osb.Remove(lastStartPos, idxEnd + closePo.Length - lastStartPos).Insert(lastStartPos, variableValue);
                                idx = lastStartPos;
                            }
                        }
                    }
                }
                if (maxDepth > 0 && startPosStack.Count > maxDepth) {
                    throw new Exception($"Max depth inclusion of {maxDepth} reached at column {idx}.");
                }
                idx++;
            } while (idx > 0 && idx <= osb.Length - 1);

            if (startPosStack.Count != 0) {
                throw new Exception($"Unbalanced number or {openPo} and {closePo}).");
            }
            
            return osb;
        }
        
        /// <summary>
        /// A simple quote to use for result display
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string PrettyQuote(this string text) {
            return $"«{text ?? ""}»";
        }

        /// <summary>
        ///     Replaces " by "", replaces new lines by spaces and add extra " at the start and end of the string
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string CliQuoter(this string text) {
            if (Utils.IsRuntimeWindowsPlatform) {
                return $"\"{text?.Replace("\"", "\"\"").Replace("\n", "").Replace("\r", "") ?? ""}\"";
            }
            return $"\"{text?.Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "") ?? ""}\"";
        }

        /// <summary>
        ///     Replaces " by "", add extra " at the start and end of the string,
        /// should be used to write a sring to a file that will be read by progress using IMPORT
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ProExportFormat(this string text) {
            return $"\"{text?.Replace("\"", "\"\"").Replace("\n", "").Replace("\r", "") ?? ""}\"";
        }

        /// <summary>
        ///  Format a text to use as a single line CHARACTER string encapsulated in double quotes ",
        /// to use in CHARACTER string definition in openedge code
        /// i.e. $"assign lc = {"toescape".ProQuoter()}"
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        /// <remarks>https://knowledgebase.progress.com/articles/Article/P27229</remarks>
        public static string ProStringify(this string text) {
            if (text == null) {
                return "?";
            }
            return $"\"{text.Replace("~", "~~").Replace("\"", "\"\"").Replace("\\", "~\\").Replace("{", "~{").Replace("\n", "~n").Replace("\r", "~r").Replace("\t", "~t")}\"";
        }

        /// <summary>
        /// Format a text to use as a single line &amp;SCOPE-DEFINE CHARACTER string encapsulated in double quotes " and with ~ escaped,
        /// to be used in a &amp;SCOPE-DEFINE definition
        /// i.e. $"&amp;SCOPE-DEFINE myvar {"toescape".ProQuoter()}"
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ProPreProcStringify(this string text) {
            return (text ?? "").ProStringify().Replace("~", "~~");
        }
        
        /// <summary>
        /// The opposite function of proquoter
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ProUnescapeString(this string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }           
            text = text.StripQuotes();
            if (text.Equals("?")) {
                return null;
            }
            return text.Replace("~~", "~").Replace("\"\"", "\"").Replace("~\\", "\\").Replace("~{", "{").Replace("~n", "\n").Replace("~r", "\r").Replace("~t", "\t");
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
        /// handle all whitespace chars not only spaces, trim both leading and trailing whitespaces, remove extra whitespaces,
        /// and all whitespaces are replaced to space char (so we have uniform space separator)
        /// Will not compact whitespaces inside quotes or double quotes
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string CliCompactWhitespaces(this string s) {
            return new StringBuilder(s).CliCompactWhitespaces().ToString();
        }
        
        /// <summary>
        /// Tests wheter or not a character is a letter from the ascii table
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static bool IsAsciiLetter(this char c) {
            return c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';
        }
    }
}