using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Oetools.Utilities.Openedge.Prolib {
    internal static class Extensions {
        /// <summary>
        /// Normalize a path with windows separators.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string NormalizeRelativePath(this string path) {
            return path.Trim().Replace('/', '\\');
        }

        /// <summary>
        /// Read the next NULL terminated <see cref="string"/> of the given <paramref name="reader"/>.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="encoding"></param>
        /// <returns>Number of bytes read.</returns>
        public static string ReadNullTerminatedString(this BinaryReader reader, Encoding encoding = null) {
            encoding = encoding ?? Encoding.ASCII;
            var bytes = new List<byte>();
            do {
                var readByte = reader.ReadByte();
                if (readByte <= 0) {
                    break;
                }

                bytes.Add(readByte);
            } while (true);

            return encoding.GetString(bytes.ToArray());
        }

        /// <summary>
        /// Write the <see cref="string"/> as byte array in the stream using given encoding (default <see cref="Encoding.ASCII"/>) and ending the string with a NULL (0) byte.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="val"></param>
        /// <param name="encoding"></param>
        /// <returns>Number of bytes written.</returns>
        public static int WriteNullTerminatedString(this BinaryWriter writer, string val, Encoding encoding = null) {
            encoding = encoding ?? Encoding.ASCII;
            var bytes = encoding.GetBytes(val);
            writer.Write(bytes, 0, bytes.Length);
            writer.Write((byte) 0); // NULL ending string
            return bytes.Length + 1;
        }

        /// <summary>
        /// Returns a datetime by adding <paramref name="secondsFromDayZero"/> to 1970.
        /// </summary>
        /// <param name="secondsFromDayZero"></param>
        /// <returns></returns>
        private static DateTime GetDatetimeFromUint(this uint secondsFromDayZero) {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secondsFromDayZero).ToLocalTime();
        }

        /// <summary>
        /// Gets the numbers of seconds between the given time and 1970.
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private static uint GetUintFromDateTime(this DateTime date) {
            return (uint) Math.Round((date.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
        }

        public static byte[] ReverseIfLittleEndian(this byte[] b) {
            if (BitConverter.IsLittleEndian) {
                Array.Reverse(b);
            }
            return b;
        }

        public static UInt16 ReadUInt16BE(this BinaryReader reader) {
            return BitConverter.ToUInt16(reader.ReadBytesRequired(sizeof(ushort)).ReverseIfLittleEndian(), 0);
        }

        public static UInt32 ReadUInt32BE(this BinaryReader reader) {
            return BitConverter.ToUInt32(reader.ReadBytesRequired(sizeof(uint)).ReverseIfLittleEndian(), 0);
        }

        public static byte[] ReadBytesRequired(this BinaryReader reader, int byteCount) {
            var result = reader.ReadBytes(byteCount);
            if (result.Length != byteCount)
                throw new EndOfStreamException(string.Format("{0} bytes required from stream, but only {1} returned.", byteCount, result.Length));
            return result;
        }
    }
}