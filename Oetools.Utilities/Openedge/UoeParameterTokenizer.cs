#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeParameterTokenizer.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Lib.ParameterStringParser;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Database.ParameterStringParser;

namespace Oetools.Utilities.Openedge {

    /// <summary>
    /// Tokenize an openedge parameter string, taking into account and reading additional -pf parameter files.
    /// </summary>
    internal class UoeParameterTokenizer {

        protected int _tokenPos = -1;

        protected List<Token> _tokenList;

        public UoeParameterTokenizer(string parameterString) {
            _tokenList = GetTokensFromParameterString(parameterString);
        }

        /// <summary>
        /// Recursive method to resolve each -pf parameter in the parameter string.
        /// </summary>
        /// <param name="parameterString"></param>
        /// <returns></returns>
        /// <exception cref="UoeConnectionStringParseException"></exception>
        private List<Token> GetTokensFromParameterString(string parameterString) {
            var tokenizer = new Tokenizer(parameterString);
            var output = new List<Token>();
            do {
                var token = tokenizer.PeekAtToken(0);
                if (token is TokenOption && token.Value.Equals("-pf", StringComparison.Ordinal)) {
                    var pfPath = tokenizer.PeekAtToken(2);
                    if (pfPath is TokenValue) {
                        var pfFilePath = pfPath.Value.StripQuotes().MakePathAbsolute();
                        if (!File.Exists(pfFilePath)) {
                            throw new UoeConnectionStringParseException($"The parameter file {pfFilePath.PrettyQuote()} does not exist but is used in the parameter string: {parameterString.PrettyQuote()}.");
                        }
                        _tokenList.AddRange(GetTokensFromParameterString(File.ReadAllText(pfFilePath)));
                    } else {
                        throw new UoeConnectionStringParseException($"Expecting a parameter file path after the -pf option in the parameter string: {parameterString.PrettyQuote()}.");
                    }
                }
                _tokenList.Add(token);
            } while (tokenizer.MoveToNextToken());
            return output;
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
        public virtual Token PeekAtToken(int x) {
            return _tokenPos + x >= _tokenList.Count || _tokenPos + x < 0 ? new TokenEof("") : _tokenList[_tokenPos + x];
        }
    }
}
