#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExtensions.cs) is part of Oetools.Utilities.
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

using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge {

    /// <summary>
    /// Extensions for openedge.
    /// </summary>
    public static class UoeExtensions {

        /// <summary>
        /// A quoter function:
        /// - surround by double quote if the text contains spaces
        /// - escape double quote with another double quote
        /// </summary>
        /// <remarks>
        /// Can be used to write a string to a file that will be read by progress using IMPORT.
        /// </remarks>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ProQuoter(this string text) {
            if (text == null) {
                return "?";
            }
            return text.ToQuotedArg();
        }

        /// <summary>
        /// Format a text to use as a single line CHARACTER string encapsulated in double quotes ",
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
            // TODO: replace this by a better method using StringBuilder like UoePfTokenizer does
            return $"\"{text.Replace("\"", "\"\"").Replace("~", "~~").Replace("\\", "~\\").Replace("{", "~{").Replace("\t", "~t").Replace("\r", "~r").Replace("\n", "~n")}\"";
        }

        /// <summary>
        /// The opposite function of <see cref="ProStringify"/>.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ProUnStringify(this string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }
            text = text.StripQuotes();
            if (text.Equals("?")) {
                return null;
            }
            return text.Replace("~n", "\n").Replace("~r", "\r").Replace("~t", "\t").Replace("~{", "{").Replace("~\\", "\\").Replace("~~", "~").Replace("\"\"", "\"");
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
        /// The opposite function of function fi_escape_special_char in oe_execution.p.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string ProUnescapeSpecialChar(this string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }
            if (text.Equals("?")) {
                return null;
            }
            return text.Replace("~n", "\n").Replace("~t", "\t");
        }
    }
}
