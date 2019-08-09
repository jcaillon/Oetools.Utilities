#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeXcode.cs) is part of Oetools.Utilities.
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
using System.Linq;
using System.Text;
using Oetools.Utilities.Openedge.Exceptions;

namespace Oetools.Utilities.Openedge {
        
    /// <summary>
    /// Implementation of the xcode utility of openedge
    /// The default key used by the tool XCODE is <see cref="DefaultXcodeKey"/>
    /// The key must be in ASCII and the maximum length used is 8 (everything above 8 is just ignored)
    /// An encrypted file is 1 byte longer than the decrypted one
    /// A file is encrypted if it starts with the byte 0x13
    /// </summary>
    public class UoeEncryptor {
        
        private const string DefaultXcodeKey = "Progress";
        private const short MaxKey = 15;
        
        private readonly ushort[] _crcTable;
        private byte[] _keyData;

        /// <summary>
        /// New encryptor using the given encryption key.
        /// </summary>
        /// <param name="key">If null, will default to Progress.</param>
        public UoeEncryptor(string key) {
            _crcTable = UoeHash.GetConstantCrcTable();
            SetKey(key);
        }

        /// <summary>
        /// Use the given encryption key.
        /// </summary>
        /// <param name="key">If null, will default to Progress.</param>
        public void SetKey(string key) {
            _keyData = GetKeyData(key);
        }

        /// <summary>
        /// Returns true if the given filepath is already encrypted
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public bool IsFileEncrypted(string filePath) {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1)) {
                var firstByte = stream.ReadByte();
                return IsFirstByteFromEncryptedFile(firstByte);
            }
        }

        /// <summary>
        /// Convert (either encrypt or decrypt) a file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encode"></param>
        /// <param name="outputFilePath"></param>
        /// <exception cref="UoeAlreadyConvertedException"></exception>
        public void ConvertFile(string filePath, bool encode, string outputFilePath) {
            var dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(outputFilePath, ConvertData(File.ReadAllBytes(filePath), encode));
        }
        
        /// <summary>
        /// Convert (either encrypt or decrypt) a byte array
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        /// <exception cref="UoeAlreadyConvertedException"></exception>
        public byte[] ConvertData(byte[] data, bool encode) {
            
            if (data == null || data.Length == 0) {
                throw new UoeAlreadyConvertedException("The data is empty, no conversion needed.");
            }
            
            var isEncoded = IsFirstByteFromEncryptedFile(data[0]);
            
            if (encode && isEncoded || !encode && !isEncoded) {
                throw new UoeAlreadyConvertedException($"The data is already {(encode ? "encoded" : "decoded")}.");
            }

            var keyData = _keyData.ToArray();
            
            int idx = 0;
            var convertedBytes = new byte[data.Length + 1];
            var crcValue = (ushort) (data[0] == 0x11 ? 0x0025 : 0x7fed);
            
            if (encode) {
                convertedBytes[0] = 0x13;
            }
            
            for (int i = encode ? 0 : 1; i < data.Length; i++) {
                var base1 = (byte) (idx++ & MaxKey);
                var keyValue = keyData[(base1 + keyData[MaxKey - base1]) & MaxKey];
                convertedBytes[idx] = (byte)(data[i] ^ keyValue);
                crcValue = (ushort) (_crcTable[(encode ? data[i] : convertedBytes[idx]) & byte.MaxValue] ^ _crcTable[crcValue & byte.MaxValue] ^ ((crcValue >> 8) & byte.MaxValue));
                crcValue = (ushort) (_crcTable[keyValue & byte.MaxValue] ^ _crcTable[crcValue & byte.MaxValue] ^ ((crcValue >> 8) & byte.MaxValue));
                keyData[base1] = (byte) (crcValue & byte.MaxValue);
            }
            
            var output = new byte[encode ? idx + 1 : idx];
            Array.Copy(convertedBytes, encode ? 0 : 1, output, 0, output.Length);
            return output;
        }
        
        private byte[] GetKeyData(string key) {
            key = key ?? DefaultXcodeKey;
            var outputKeyBytes = new byte[MaxKey + 1];
            
            var keyBytes = new byte[8];
            var stringBytes = Encoding.ASCII.GetBytes(key);
            Array.Copy(stringBytes, keyBytes, stringBytes.Length > 8 ? 8 : stringBytes.Length);
            
            for (int i = 0; i < keyBytes.Length; i++) {
                int offset = (i - keyBytes[3]) & 7;
                outputKeyBytes[offset] = (byte)(keyBytes[i] == 0 ? 17 + offset : keyBytes[i] + i);
                outputKeyBytes[i + 8] = (byte)(outputKeyBytes[offset] ^ keyBytes[(i - 5) & 7]);
            }
            
            return outputKeyBytes;
        }
        
        private static bool IsFirstByteFromEncryptedFile(int firstByte) => (firstByte | 2) == 0x13;
        
    }
}

