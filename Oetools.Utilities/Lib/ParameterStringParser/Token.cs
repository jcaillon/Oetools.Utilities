#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (Token.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Lib.ParameterStringParser {

    /// <summary>
    /// Token object
    /// </summary>
    internal abstract class Token {
        public string Value { get; private set; }

        protected Token(string value) {
            Value = value;
        }
    }

    internal class TokenOption : Token {
        public TokenOption(string value) : base(value) {}
    }

    internal class TokenValue : Token {
        public TokenValue(string value) : base(value) {}
    }

    internal class TokenWhiteSpace : Token {
        public TokenWhiteSpace(string value) : base(value) {}
    }

    /// <summary>
    /// Eof is end of file/string.
    /// </summary>
    internal class TokenEof : Token {
        public TokenEof(string value) : base(value) {}
    }
}
