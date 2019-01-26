#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (Tokenizer.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Lib.ParameterStringParser {

    /// <summary>
    /// This class "tokenize" the input data into tokens of various types,
    /// it implements a visitor pattern
    /// </summary>
    internal class ParameterStringTokenizer {

        protected const char Eof = (char) 0;

        protected string _data;
        protected int _dataLength;
        protected int _pos;

        protected int _startPos;

        protected int _tokenPos = -1;

        protected List<ParameterStringToken> _tokenList;

        /// <summary>
        /// Returns the tokens list
        /// </summary>
        public List<ParameterStringToken> TokensList {
            get { return _tokenList; }
        }

        /// <summary>
        /// constructor, data is the input string to tokenize
        /// call Tokenize() to do the work
        /// </summary>
        public ParameterStringTokenizer(string data) {
            Construct(data);
        }

        protected void Construct(string data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            _data = data;
            _dataLength = _data.Length;

            Tokenize();
        }

        /// <summary>
        /// Move the cursor to the first token.
        /// </summary>
        public virtual void MoveToFirstToken() {
            _tokenPos = 0;
        }

        /// <summary>
        /// To use this lexer as an enumerator,
        /// Move to the next token, return true if it can
        /// </summary>
        public virtual bool MoveToNextToken() {
            return ++_tokenPos < _tokenList.Count;
        }

        /// <summary>
        /// To use this lexer as an enumerator,
        /// peek at the current pos + x token of the list, returns a new TokenEof if can't find
        /// </summary>
        public virtual ParameterStringToken PeekAtToken(int x) {
            return _tokenPos + x >= _tokenList.Count || _tokenPos + x < 0 ? new ParameterStringTokenEof("") : _tokenList[_tokenPos + x];
        }

        /// <summary>
        /// To use this lexer as an enumerator,
        /// peek at the current pos + x token of the list, returns a new TokenEof if can't find
        /// </summary>
        public virtual ParameterStringToken MoveAndPeekAtToken(int x) {
            _tokenPos += x;
            return _tokenPos >= _tokenList.Count || _tokenPos < 0 ? new ParameterStringTokenEof("") : _tokenList[_tokenPos];
        }

        /// <summary>
        /// Call this method to actually tokenize the string
        /// </summary>
        protected void Tokenize() {
            if (_data == null)
                return;

            if (_tokenList == null) {
                _tokenList = new List<ParameterStringToken>();
            }

            ParameterStringToken parameterStringToken;
            do {
                parameterStringToken = GetNextToken();
                _tokenList.Add(parameterStringToken);
            } while (!(parameterStringToken is ParameterStringTokenEof));

            // clean
            _data = null;
        }

        /// <summary>
        /// Peek forward x chars
        /// </summary>
        protected char PeekAtChr(int x) {
            return _pos + x >= _dataLength ? Eof : _data[_pos + x];
        }

        /// <summary>
        /// peek backward x chars
        /// </summary>
        protected char PeekAtChrReverse(int x) {
            return _pos - x < 0 ? Eof : _data[_pos - x];
        }

        /// <summary>
        /// Read to the next char,
        /// indirectly adding the current char (_data[_pos]) to the current token
        /// </summary>
        protected void ReadChr() {
            _pos++;
        }

        /// <summary>
        /// Returns the current value of the token
        /// </summary>
        /// <returns></returns>
        protected string GetTokenValue() {
            return _data.Substring(_startPos, _pos - _startPos);
        }

        /// <summary>
        /// returns the next token of the string
        /// </summary>
        /// <returns></returns>
        protected ParameterStringToken GetNextToken() {
            _startPos = _pos;

            var ch = PeekAtChr(0);

            // END OF FILE reached
            if (ch == Eof)
                return new ParameterStringTokenEof(GetTokenValue());

            if (char.IsWhiteSpace(ch)) {
                return CreateWhitespaceToken();
            }
            return IsOptionCharacter(ch) ? CreateOptionToken() : CreateValueToken(ch == '"');
        }

        protected virtual bool IsOptionCharacter(char ch) {
            return ch == '-';
        }

        protected virtual ParameterStringToken CreateWhitespaceToken() {
            ReadChr();
            while (true) {
                var ch = PeekAtChr(0);
                if (ch == '\t' || ch == ' ' || ch == '\r' || ch == '\n')
                    ReadChr();
                else
                    break;
            }
            return new ParameterStringTokenWhiteSpace(GetTokenValue());
        }

        protected virtual ParameterStringToken CreateValueToken(bool quotedValue) {
            ReadChr();
            while (true) {
                var ch = PeekAtChr(0);
                if (ch == Eof)
                    break;

                // quote char
                if (quotedValue && ch == '"') {
                    ReadChr();
                    if (PeekAtChr(0) == '"') {
                        ReadChr();
                        continue;
                    }
                    break; // done reading
                }

                if (!quotedValue && char.IsWhiteSpace(ch)) {
                    break;
                }

                ReadChr();
            }
            return new ParameterStringTokenValue(GetTokenValue());
        }

        protected virtual ParameterStringToken CreateOptionToken() {
            ReadChr();
            while (true) {
                var ch = PeekAtChr(0);
                if (ch == Eof)
                    break;

                // normal word
                if (!char.IsWhiteSpace(ch)) {
                    ReadChr();
                    continue;
                }
                break;
            }
            return new ParameterStringTokenOption(GetTokenValue());
        }
    }
}
