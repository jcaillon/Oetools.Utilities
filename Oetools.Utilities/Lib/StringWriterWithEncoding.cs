#region header

// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (StringWriterWithEncoding.cs) is part of Oetools.Utilities.
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

using System.IO;
using System.Text;

namespace Oetools.Utilities.Lib {

    /// <summary>
    /// A <see cref="StringWriter"/> class with encoding selection.
    /// </summary>
    public sealed class StringWriterWithEncoding : StringWriter {
        private readonly Encoding _encoding;

        public StringWriterWithEncoding(StringBuilder sb) : base(sb) {
            _encoding = Encoding.UTF8;
        }

        public StringWriterWithEncoding(Encoding encoding) {
            _encoding = encoding;
        }

        public StringWriterWithEncoding(StringBuilder sb, Encoding encoding) : base(sb) {
            _encoding = encoding;
        }

        public override Encoding Encoding => _encoding;
    }
}
