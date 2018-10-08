#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeHash.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Openedge {

    /// <summary>
    /// Implementation (by pvginkel) of the wrongly named ENCODE function of openedge
    /// </summary>
    public class UoeHash {
        
        private static ushort[] _lookupTable;
        
        /// <summary>
        /// Implementation of the wrongly named ENCODE function of openedge which is actually a hash function
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string Hash(byte[] input) {
            
            if (input == null) {
                throw new ArgumentNullException(nameof(input));
            }

            if (_lookupTable == null) {
                _lookupTable = UoeEncryptor.GetConstantLookupTable();
            }

            byte[] scratch = new byte[16];

            ushort hash = 17;

            for (int i = 0; i < 5; i++) {
                for (int j = 0; j < input.Length; j++) {
                    scratch[15 - j % 16] ^= input[j];
                }

                for (int j = 0; j < 16; j += 2) {
                    hash = Hash(scratch, hash);

                    scratch[j] = (byte) (hash & 0xff);
                    scratch[j + 1] = (byte) ((hash >> 8) & 0xff);
                }
            }

            byte[] target = new byte[16];

            for (int i = 0; i < 16; i++) {
                byte lower = (byte) (scratch[i] & 0x7f);

                if (lower >= 'A' && lower <= 'Z' || lower >= 'a' && lower <= 'z')
                    target[i] = lower;
                else
                    target[i] = (byte) ((scratch[i] >> 4) + 0x61);
            }

            return Encoding.ASCII.GetString(target);
        }

        private static ushort Hash(byte[] scratch, ushort hash) {
            for (int i = 15; i >= 0; i--) {
                hash = (ushort) (hash >> 8 ^ _lookupTable[hash & 0xff] ^ _lookupTable[scratch[i]]);
            }
            return hash;
        }
    }
}