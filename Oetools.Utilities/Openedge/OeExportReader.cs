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
using System.IO;
using System.Text;

namespace Oetools.Utilities.Openedge {

    public class OeExportReader : IDisposable {

        private IOeExportStream _stream;
        private int _recordNb;
        private int _fieldNb;
        private int _fieldStartPosition;
        private int _nbPreviousQuotes;
        private bool _inQuote;
        private ReadType _type;

        public OeExportReader(string inputString) {
            _stream = new OeStringStream(inputString);
        }

        public OeExportReader(string filePath, Encoding encoding) {
            _stream = new OeFileStream(filePath, encoding);
        }

        public void Dispose() {
            _stream?.Dispose();
        }

        public void Reset() {
            _type = ReadType.NewLine;
            _recordNb = -1;
            _fieldNb = -1;
            _fieldStartPosition = 0;
            _inQuote = false;
            _nbPreviousQuotes = 0;
        }

        public int RecordNumber => _recordNb;

        public int RecordFieldNumber => _fieldNb;

        public string RecordValue {
            get {
                var recordLength = _stream.Position - _fieldStartPosition - 1;
                _stream.Position = _fieldStartPosition;
                var fieldValue = recordLength > 0 ? _stream.Read(recordLength) : null;
                _stream.Position++;
                return fieldValue;
            }
        }
        
        public bool ReadNextRecordField() {
            _fieldNb++;
            if (_type == ReadType.NewLine) {
                _recordNb++;
                _fieldNb = 0;
            }
            _fieldStartPosition = _stream.Position;
            do {
                _type = _stream.Read();
                switch (_type) {
                    case ReadType.EndOfStream:
                    case ReadType.WhiteSpace:
                    case ReadType.NewLine:
                        if (_inQuote && _nbPreviousQuotes % 2 == 0) {
                            _nbPreviousQuotes = 0;
                            continue;
                        }
                        _inQuote = false;
                        _nbPreviousQuotes = 0;
                        // is record length > 0
                        if (_stream.Position - _fieldStartPosition - 1 > 0) {
                            return true;
                        }
                        _fieldStartPosition = _stream.Position;
                        break;
                    case ReadType.DoubleQuote: // double quote
                        _nbPreviousQuotes++;
                        _inQuote = true;
                        break;
                    default:
                        _nbPreviousQuotes = 0;
                        break;
                }
            } while (_type != ReadType.EndOfStream);

            return false;
        }

        private enum ReadType {
            NewLine,
            WhiteSpace,
            DoubleQuote,
            Default,
            EndOfStream
        }

        private interface IOeExportStream : IDisposable {
            /// <summary>
            /// Advanced the read position of 1, returns the type of read
            /// </summary>
            /// <returns></returns>
            ReadType Read();

            /// <summary>
            /// Get or set the read position
            /// </summary>
            int Position { get; set; }

            /// <summary>
            /// Read from current position for given length, advances the read position
            /// </summary>
            /// <param name="length"></param>
            /// <returns></returns>
            string Read(int length);
        }

        private class OeFileStream : FileStream, IOeExportStream {
            private Encoding _encoding;

            public OeFileStream(string path, Encoding encoding) : base(path, FileMode.Open, FileAccess.Read, FileShare.Read) {
                _encoding = encoding;
            }

            public ReadType Read() {
                switch (ReadByte()) {
                    case -1:
                        return ReadType.EndOfStream;
                    case 34: // "
                        return ReadType.DoubleQuote;
                    case 10: // \n
                    case 13: // \r
                        return ReadType.NewLine;
                    case 9: // tab
                    case 32: // space
                        return ReadType.WhiteSpace;
                    default:
                        return ReadType.Default;
                }
            }

            public new int Position {
                get => (int) base.Position;
                set => base.Position = value;
            }

            public string Read(int length) {
                var buffer = new byte[length];
                Read(buffer, 0, length);
                return _encoding.GetString(buffer);
            }
        }

        private class OeStringStream : IOeExportStream {
            private readonly string _input;

            private int _position;

            public OeStringStream(string input) {
                _input = input;
            }

            public ReadType Read() {
                if (_position >= _input.Length) {
                    return ReadType.EndOfStream;
                }

                switch (_input[_position++]) {
                    case '"': // "
                        return ReadType.DoubleQuote;
                    case '\n': // \n
                    case '\r': // \r
                        return ReadType.NewLine;
                    case '\t': // tab
                    case ' ': // space
                        return ReadType.WhiteSpace;
                    default:
                        return ReadType.Default;
                }
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

            public void Dispose() { }
        }

//                do {
//                    byteRead = fileStream.Read(buffer, bufferPositon, bufferSize);
//                    if (byteRead == 0) {
//                        break;
//                    }
//
//                    bufferPositon += byteRead;
//                    for (int i = 0; i < byteRead; i++) {
//                        switch (buffer[i]) {
//                            case 10: // \n
//
//                                break;
//                            case 32: // space
//
//                                break;
//                            case 34: // double quote
//
//                                break;
//
//                        }
//                    }
//                } while (byteRead > 0);
    }
}