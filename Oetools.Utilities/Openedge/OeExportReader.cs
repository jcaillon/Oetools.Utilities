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

    /// <summary>
    /// This class is made to a file or string formatted to contain records (and each record contains fields)
    /// This is the typical case of a .d file in openedge, which contains EXPORT data
    /// fields are separated by spaces (space, tab and \r)
    /// recards are separated by new lines (\n)
    /// A field can contain spaces but is has to be double quoted (e.g. "field with spaces")
    /// A double quoted field can contain double quotes but they must be doubled (e.g. "field ""with quotes""")
    /// (note : "" appear as " once read with this class)
    /// </summary>
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
        
        /// <summary>
        /// Get the current record number, only when moving between fields with <see cref="MoveToNextRecordField"/>
        /// </summary>
        public int RecordNumber => _recordNb;

        /// <summary>
        /// Get the current field number within the current record, only when moving between fields with <see cref="MoveToNextRecordField"/>
        /// </summary>
        public int RecordFieldNumber => _fieldNb;

        /// <summary>
        /// Get the current record value (if the string was double quoted, they will be kept at the beggining and the end of the string)
        /// only when moving with <see cref="MoveToNextRecordField"/>
        /// </summary>
        /// <remarks>
        /// for instance, a record read :
        ///    "my ""cool"" record"
        /// will output :
        ///    "my "cool" record"
        /// </remarks>
        public string RecordValue => GetRecordValueInternal(false);

        /// <summary>
        /// Get the current record value (if the string was double quoted, the begin/end quotes will NOT be kept)
        /// only when moving with <see cref="MoveToNextRecordField"/>
        /// </summary>
        /// <remarks>
        /// for instance, a record read :
        ///    "my ""cool"" record"
        /// will output :
        ///    my "cool" record
        /// </remarks>
        public string RecordValueNoQuotes => GetRecordValueInternal(true);

        /// <summary>
        /// Tries to move to the next record field, false if it can't (= end of stream)
        /// Use public properties like <see cref="RecordNumber"/>, <see cref="RecordValue"/> to get the record you moved to
        /// </summary>
        /// <returns></returns>
        public bool MoveToNextRecordField() {
            return MoveToNextRecordFieldInternal();
        }
        
        /// <summary>
        /// Reads a entire record and output it, returns false when reaching the end of the stream
        /// (you should not use public properties of this class when using this method as they will not output what you expect)
        /// </summary>
        /// <param name="record"></param>
        /// <param name="recordNumber"></param>
        /// <param name="noQuotesValues"></param>
        /// <returns></returns>
        public bool ReadRecord(out List<string> record, out int recordNumber, bool noQuotesValues = false) {
            record = new List<string>();
            if (_recordNb > 0 && _type != ReadType.EndOfStream) {
                record.Add(noQuotesValues ? RecordValueNoQuotes : RecordValue);
            }
            recordNumber = _recordNb;
            do {
                var canRead = MoveToNextRecordFieldInternal();
                if (canRead && _recordNb == recordNumber) {
                    record.Add(noQuotesValues ? RecordValueNoQuotes : RecordValue);
                } else {
                    break;
                }
            } while (true);
            return record.Count > 0;
        }

        private bool MoveToNextRecordFieldInternal() {
            _fieldNb++;
            _fieldStartPosition = _stream.Position;
            do {
                if (_type == ReadType.NewLine) {
                    _recordNb++;
                    _fieldNb = 0;
                }
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
            bool endsWithQuote = false;
            if (stripQuotes) {
                if (_stream.Peek(0) == '"') {
                    _stream.Position++;
                    recordLength--;
                }
                if (_stream.Peek(recordLength - 1) == '"') {
                    recordLength--;
                    endsWithQuote = true;
                }
            }
            var fieldValue = recordLength > 0 ? _stream.Read(recordLength) : string.Empty;
            _stream.Position++;
            if (stripQuotes && endsWithQuote) {
                _stream.Position++;
            }
            // replace "" by " if needed
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
            
            private char[] _endOfFieldChars = { ' ', '\t', '\r', '\n', '"' };
            
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
                            case '\r':
                            case '\t':
                            case ' ':
                                return ReadType.WhiteSpace;
                            case '"':
                                return ReadType.DoubleQuote;
                            case '\n':
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