#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeProMsg.cs) is part of Oetools.Utilities.
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
#region header
// ========================================================================
// Copyright (c) 2019 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeProMsg.cs) is part of Oetools.Utilities.
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
using System.Linq;
using System.Text;
using DotUtilities.Extensions;

namespace Oetools.Utilities.Openedge {

    /// <summary>
    /// Represents an openedge prosmg
    /// </summary>
    public class UoeProMessage {

        /// <summary>
        /// Error number.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Error text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// The description of the error/message.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The fist letters of the category if this message.
        /// </summary>
        public string CategoryFirstLetters { get; set; }

        /// <summary>
        /// Urls to the knowledge base if any.
        /// </summary>
        public string KnowledgeBase { get; set; }

        public string Category {
            get {
                var categories = new List<string> {
                    "Compiler",
                    "Database",
                    "Index",
                    "Miscellaneous",
                    "Operating System",
                    "Program/Execution",
                    "Syntax"
                };
                return categories.FirstOrDefault(c => c.StartsWith(CategoryFirstLetters, StringComparison.OrdinalIgnoreCase));
            }
        }

        public override string ToString() {
            var cat = Category;
            return $"{(cat != null ? $"({cat}) " : "")}{Description}{(KnowledgeBase.Length > 2 ? $" ({KnowledgeBase.StripQuotes()})" : "")}";
        }

        /// <summary>
        /// Returns the detailed message found in the prohelp folder of dlc corresponding to the given error number
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="errorNumber"></param>
        /// <returns></returns>
        public static UoeProMessage GetProMessage(string dlcPath, int errorNumber) {
            var messageDir = Path.Combine(dlcPath, "prohelp", "msgdata");
            if (!Directory.Exists(messageDir)) {
                return null;
            }

            var messageFile = Path.Combine(messageDir, $"msg{(errorNumber - 1) / 50 + 1}");
            if (!File.Exists(messageFile)) {
                return null;
            }

            UoeProMessage outputMessage = null;

            var err = errorNumber.ToString();
            using (var reader = new UoeExportReader(messageFile, Encoding.Default)) {
                while (reader.MoveToNextRecordField()) {
                    if (reader.RecordFieldNumber == 0 && reader.RecordValue == err) {
                        outputMessage = new UoeProMessage {
                            Number = errorNumber,
                            Text = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty,
                            Description = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty,
                            CategoryFirstLetters = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty,
                            KnowledgeBase = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty
                        };
                        break;
                    }
                }
            }

            return outputMessage;
        }
    }
}
