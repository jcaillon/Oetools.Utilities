#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoePfParser.cs) is part of Oetools.Utilities.
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
using System.Text;
using DotUtilities;
using DotUtilities.ParameterString;

namespace Oetools.Utilities.Openedge {

    /// <summary>
    /// Tokenize the content of a .pf file.
    /// </summary>
    /// <remarks>
    /// Pf format:
    /// https://documentation.progress.com/output/ua/OpenEdge_latest/index.html#page/dpspr/parameter-file-format.html
    /// </remarks>
    public class UoePfTokenizer : ParameterStringTokenizer {

        /// <summary>
        /// New instance, immediately tokenize the <paramref name="data"/> string.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="isWindowsPlatform"></param>
        /// <returns></returns>
        public static UoePfTokenizer New(string data, bool? isWindowsPlatform = null) {
            var obj = new UoePfTokenizer(isWindowsPlatform ?? Utils.IsRuntimeWindowsPlatform);
            obj.Start(data);
            return obj;
        }

        private char _escapeChar;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isWindowsPlatform"></param>
        protected UoePfTokenizer(bool isWindowsPlatform) {
            _escapeChar = isWindowsPlatform ? '~' : '\\';
        }

        protected override ParameterStringToken GetNextToken() {
            var ch = PeekAtChr(0);

            // END OF FILE reached
            if (ch == Eof) {
                return new ParameterStringTokenEof(GetTokenValue());
            }

            if (char.IsWhiteSpace(ch)) {
                return CreateWhitespaceToken();
            }

            if (ch == '#') {
                return CreateCommentToken();
            }

            return CreateToken(ch);
        }

        private ParameterStringTokenComment CreateCommentToken() {
            ReadChr();
            while (true) {
                var ch = PeekAtChr(0);
                if (ch != '\r' && ch != '\n')
                    ReadChr();
                else
                    break;
            }
            return new ParameterStringTokenComment(GetTokenValue());
        }

        protected override ParameterStringToken CreateToken(char ch) {
            var openedQuote = false;
            var sb = new StringBuilder();

            while (true) {
                ch = PeekAtChr(0);
                if (ch == Eof) {
                    break;
                }

                // escape char
                if (ch == _escapeChar) {
                    ReadChr();
                    ch = PeekAtChr(0);
                    switch (ch) {
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'E':
                            sb.Append((char) 27);
                            break;
                        case 'b':
                            sb.Append((char) 8);
                            break;
                        case 'f':
                            sb.Append((char) 12);
                            break;
                        default: {
                            if (char.IsDigit(ch) && char.IsDigit(PeekAtChr(1)) && char.IsDigit(PeekAtChr(2))) {
                                // ~nnn Where nnn is an octal value between 000 and 377.
                                sb.Append((char) Convert.ToInt32($"{ch}{PeekAtChr(1)}{PeekAtChr(2)}", 8));
                                ReadChr();
                                ReadChr();
                            } else {
                                sb.Append(ch);
                            }

                            break;
                        }
                    }
                    ReadChr();
                    continue;
                }

                // quote char
                if (ch == '"' || ch == '\'') {
                    openedQuote = !openedQuote;
                    ReadChr();
                    continue;
                }

                if (!openedQuote && (char.IsWhiteSpace(ch) || ch == '#')) {
                    break;
                }

                sb.Append(ch);
                ReadChr();
            }

            var value = sb.ToString();
            return IsOptionCharacter(value[0]) ? (ParameterStringToken) new ParameterStringTokenOption(value) : new ParameterStringTokenValue(value);
        }
    }

    /// <summary>
    /// An comment token.
    /// </summary>
    public class ParameterStringTokenComment : ParameterStringToken {

        /// <inheritdoc />
        public ParameterStringTokenComment(string value) : base(value) {}
    }
}
