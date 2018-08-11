#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (OeExportReader.cs) is part of Oetools.Utilities.
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
using System.Text;

namespace Oetools.Utilities.Openedge {

    public class OeExportReader : IDisposable {

        private IOeExportStream _stream;
        private int _recordNb;
        private int _fieldNb = -1;
        private int _fieldStartPosition;
        private ReadType _type = ReadType.EndOfStream;

        public OeExportReader(string inputString) {
            _stream = new OeStringStream(inputString);
        }

        public OeExportReader(string filePath, Encoding encoding) {
            _stream = new OeStringStream(File.ReadAllText(filePath, encoding));
        }

        public void Dispose() {
            _stream?.Dispose();
        }
        
        public int RecordNumber => _recordNb;

        public int RecordFieldNumber => _fieldNb;

        public string RecordValue => GetRecordValueInternal(false);

        public string RecordValueNoQuotes => GetRecordValueInternal(true);

        public bool ReadNextRecordField() {
            return ReadNextRecordFieldInternal();
        }
        
        public List<string> GetNextRecord(bool noQuotesValues = false) {
            var output = new List<string>();
            var initialRecordNb = _recordNb;
            bool canRead;
            do {
                canRead = ReadNextRecordFieldInternal();
                if (canRead) {
                    output.Add(noQuotesValues ? RecordValueNoQuotes : RecordValue);
                }
            } while (canRead && _recordNb == initialRecordNb);
            return output.Count > 0 ? output : null;
        }

        public bool ReadNextRecord() {
            var initialRecordNb = _recordNb;
            bool canRead;
            do {
                canRead = ReadNextRecordFieldInternal();
            } while (canRead && _recordNb == initialRecordNb);
            return canRead;
        }

        private bool ReadNextRecordFieldInternal() {
            _fieldNb++;
            if (_type == ReadType.NewLine) {
                _recordNb++;
                _fieldNb = 0;
            }
            _fieldStartPosition = _stream.Position;
            do {
                _type = _stream.ReadToNextFieldEnd();
                switch (_type) {
                    case ReadType.EndOfStream:
                    case ReadType.WhiteSpace:
                    case ReadType.NewLine:
                        // is record length > 0
                        if (_stream.Position - _fieldStartPosition - 1 > 0) {
                            return true;
                        }
                        _fieldStartPosition = _stream.Position;
                        break;
                    case ReadType.DoubleQuote: // double quote
                        _stream.ReadToNextQuotes();
                        break;
                }
            } while (_type != ReadType.EndOfStream);
            return false;
        }        
        
        private string GetRecordValueInternal(bool stripQuotes) {
            var recordLength = _stream.Position - _fieldStartPosition - 1;
            _stream.Position = _fieldStartPosition;
            if (stripQuotes) {
                if (_stream.Peek(0) == '"') {
                    _stream.Position++;
                    recordLength--;
                }
                if (_stream.Peek(recordLength - 1) == '"') {
                    recordLength--;
                }
            }
            var fieldValue = recordLength > 0 ? _stream.Read(recordLength) : string.Empty;
            _stream.Position++;
            var quotePos = fieldValue.IndexOf('"', stripQuotes ? 0 : 1);
            return quotePos >= 0 && quotePos < fieldValue.Length - (stripQuotes ? 1 : 2) ? fieldValue.Replace("\"\"", "\"") : fieldValue;
        }


        private enum ReadType {
            NewLine,
            WhiteSpace,
            DoubleQuote,
            EndOfStream
        }

        private interface IOeExportStream : IDisposable {

            /// <summary>
            /// Get or set the read position
            /// </summary>
            int Position { get; set; }

            /// <summary>
            /// Read from current position for given length, advances the read position to the end of the read portion
            /// </summary>
            /// <param name="length"></param>
            /// <returns></returns>
            string Read(int length);

            ReadType ReadToNextFieldEnd();
            
            void ReadToNextQuotes();

            char Peek(int offset);
        }

        private class OeStringStream : IOeExportStream {
            
            private char[] _endOfFieldChars = { ' ', '\t', '\n', '"' };
            
            private string _input;

            private int _position;

            public OeStringStream(string input) {
                _input = input ?? string.Empty;
            }

            public int Position {
                get => _position;
                set => _position = value;
            }

            public string Read(int length) {
                var str = _input.Substring(_position, length);
                _position += length;
                return str;
            }

            public ReadType ReadToNextFieldEnd() {
                if (_position <= _input.Length - 1) {
                    _position = _input.IndexOfAny(_endOfFieldChars, _position);
                    if (_position >= 0) {
                        switch (_input[_position++]) {
                            case '\t': // tab
                            case ' ': // space
                                return ReadType.WhiteSpace;
                            case '"': // "
                                return ReadType.DoubleQuote;
                            case '\n': // \n
                                return ReadType.NewLine;
                        }
                    }
                }
                _position = _input.Length + 1;
                return ReadType.EndOfStream;
            }

            public void ReadToNextQuotes() {
                if (_position <= _input.Length - 1) {
                    _position = _input.IndexOf('"', _position);
                    if (_position++ >= 0) {
                        return;
                    }
                }
                _position = _input.Length;
            }

            public char Peek(int offset) {
                var pos = _position + offset;
                return pos >= 0 && pos <= _input.Length - 1 ? _input[pos] : (char) 0;
            }

            public void Dispose() {
                _input = null;
            }
        }
    }
}