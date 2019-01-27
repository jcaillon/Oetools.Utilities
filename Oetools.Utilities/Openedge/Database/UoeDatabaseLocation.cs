#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeDatabase.cs) is part of Oetools.Utilities.
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
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Database {

    /// <summary>
    /// An openedge database location.
    /// </summary>
    public class UoeDatabaseLocation {

        public const string Extension = ".db";
        public const string StructureFileExtension = ".st";
        private const int DbPhysicalNameMaxLength = 11;
        private const int DbLogicalNameMaxLength = 32;

        /// <summary>
        /// The full directory path in which the database is located (w/o the ending dir separator).
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// The physical (file) name of the database (without the .db extension).
        /// </summary>
        public string PhysicalName { get; }

        /// <summary>
        /// The full path to the .db file of the database.
        /// </summary>
        public string FullPath => Path.Combine(DirectoryPath, $"{PhysicalName}{Extension}");

        /// <summary>
        /// The full path to the .st file of the database.
        /// </summary>
        public string StructureFileFullPath => Path.Combine(DirectoryPath, $"{PhysicalName}{StructureFileExtension}");

        /// <summary>
        /// New instance of <see cref="UoeDatabaseLocation"/> from another file path. For instance, the .df or the .st file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="UoeDatabaseException"></exception>
        public static UoeDatabaseLocation FromOtherFilePath(string filePath) {
            var dir = Path.GetDirectoryName(filePath);
            var physicalName = GetValidPhysicalName(Path.GetFileNameWithoutExtension(filePath));
            return new UoeDatabaseLocation(string.IsNullOrEmpty(dir) ? physicalName : Path.Combine(dir, physicalName));
        }

        /// <summary>
        /// New instance using the path to the .db file (the .db extension can be omitted). The path can be relative to the current directory.
        /// </summary>
        /// <param name="databasePath"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public UoeDatabaseLocation(string databasePath) {
            if (string.IsNullOrEmpty(databasePath)) {
                throw new UoeDatabaseException("Invalid path, can't be null.");
            }

            databasePath = databasePath.ToAbsolutePath();
            DirectoryPath = Path.GetDirectoryName(databasePath);

            if (string.IsNullOrEmpty(DirectoryPath)) {
                throw new UoeDatabaseException("The database folder can't be null.");
            }

            if (!Directory.Exists(DirectoryPath)) {
                Directory.CreateDirectory(DirectoryPath);
            }

            PhysicalName = Path.GetFileName(databasePath);

            if (string.IsNullOrEmpty(PhysicalName)) {
                throw new UoeDatabaseException($"The physical name of the database is empty: {databasePath.PrettyQuote()}.");
            }

            if (PhysicalName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) {
                PhysicalName = PhysicalName.Substring(0, PhysicalName.Length - Extension.Length);
            }

            if (PhysicalName.Any(c => !c.IsAsciiLetter() && !char.IsDigit(c) && c != '_' && c != '-')) {
                throw new UoeDatabaseException($"The physical name of the database contains forbidden characters (should only contain english letters and numbers, underscore (_), and dash (-) characters): {PhysicalName.PrettyQuote()}.");
            }

            if (PhysicalName.Length > DbPhysicalNameMaxLength) {
                throw new UoeDatabaseException($"The physical name of the database is too long (>{DbPhysicalNameMaxLength}): {PhysicalName.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Does the database exists? (checks .db file)
        /// </summary>
        /// <returns></returns>
        public bool Exists() {
            return File.Exists(FullPath);
        }

        /// <summary>
        /// Throws an exception if the .db file does not exist.
        /// </summary>
        /// <exception cref="UoeDatabaseException"></exception>
        public void ThrowIfNotExist() {
            if (!Exists()) {
                throw new UoeDatabaseException($"The database doesn't exist in the following location: {FullPath.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// A string representation of a database location.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => FullPath;

        /// <summary>
        /// Throws exceptions if the given logical name is invalid
        /// </summary>
        /// <param name="logicalName"></param>
        /// <exception cref="UoeDatabaseException"></exception>
        public static void ValidateLogicalName(string logicalName) {
            if (string.IsNullOrEmpty(logicalName)) {
                throw new UoeDatabaseException("The logical name of the database is null or empty.");
            }
            if (logicalName.Length > DbLogicalNameMaxLength) {
                throw new UoeDatabaseException($"The logical name of the database is too long (>{DbLogicalNameMaxLength}): {logicalName.PrettyQuote()}.");
            }
            if (logicalName.Any(c => !c.IsAsciiLetter() && !char.IsDigit(c) && c != '_' && c != '-')) {
                throw new UoeDatabaseException($"The logical name of the database contains forbidden characters (should only contain english letters and numbers, underscore (_), and dash (-) characters) : {logicalName.PrettyQuote()}.");
            }
            if (!logicalName[0].IsAsciiLetter()) {
                throw new UoeDatabaseException($"The logical name of a database should start with a english letter: {logicalName.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Returns a valid logical name from a string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetValidLogicalName(string input) {
            if (string.IsNullOrEmpty(input)) {
                return "unnamed";
            }
            var output = new StringBuilder();
            foreach (var character in input) {
                if (character.IsAsciiLetter() || char.IsDigit(character) || character == '_' || character == '-') {
                    output.Append(character);
                }
                if (output.Length >= DbLogicalNameMaxLength) {
                    break;
                }
            }
            return output.Length > 0 ? output.ToString() : "unnamed";
        }

        /// <summary>
        /// Returns a valid physical name from a string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetValidPhysicalName(string input) {
            var output = GetValidLogicalName(input);
            return output.Length > DbPhysicalNameMaxLength ? output.Substring(0, DbPhysicalNameMaxLength) : output;
        }

    }
}
