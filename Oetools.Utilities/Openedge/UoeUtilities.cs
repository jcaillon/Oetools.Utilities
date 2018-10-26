#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeUtilities.cs) is part of Oetools.Utilities.
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
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Exceptions;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Openedge {
    public static class UoeUtilities {
        
        /// <summary>
        /// Reads an openedge log file (that should contain the FILEID trace type) and output all the files
        /// that were opened during the log session
        /// When activating logs before a compilation, this can be a safe way to get ALL the includes
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static HashSet<string> GetReferencedFilesFromFileIdLog(string filePath, Encoding encoding) {
            
            // we want to read this kind of line :
            // [17/04/09@16:44:14.372+0200] P-009532 T-007832 2 4GL FILEID   Open E:\Common\CommonObj.i ID=33
            // [17/04/09@16:44:14.372+0200] P-009532 T-007832 2 4GL FILEID   Open E:\Common space\CommonObji.cls ID=33
            var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new UoeExportReader(filePath, encoding)) {
                while (reader.MoveToNextRecordField()) {
                    if (reader.RecordFieldNumber != 5 || reader.RecordValue != "FILEID") {
                        continue;
                    }
                    var currentRecordNumber = reader.RecordNumber;
                    if (!reader.MoveToNextRecordField() || currentRecordNumber != reader.RecordNumber || reader.RecordValue != "Open") {
                        continue;
                    }
                    if (!reader.MoveToNextRecordField() || currentRecordNumber != reader.RecordNumber) {
                        continue;
                    }
                    string foundRef = reader.RecordValue; // E:\Common (or directly E:\Common\CommonObj.i)
                    if (!reader.MoveToNextRecordField() || currentRecordNumber != reader.RecordNumber) {
                        continue;
                    }
                    string lastString = reader.RecordValue; // space\CommonObji.cls (or directly ID=33)
                    do {
                        if (!reader.MoveToNextRecordField() || currentRecordNumber != reader.RecordNumber) {
                            break;
                        }
                        foundRef = $"{foundRef} {lastString}";
                        lastString = reader.RecordValue;
                    } while (true);
                    if (!references.Contains(foundRef)) {
                        references.Add(foundRef);
                    }
                }
            }
            return references;
        }

        /// <summary>
        /// Reads an xref file (generated during compilation) and outputs a list of referenced tables and sequences
        /// This list can then be used to know the dependencies of a given file which in turns help you
        /// know which file needs to be recompiled when a table is modified (or sequence deleted)
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static HashSet<string> GetDatabaseReferencesFromXrefFile(string filePath, Encoding encoding) {
            var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new UoeExportReader(filePath, encoding)) {
                while (reader.MoveToNextRecordField()) {
                    if (reader.RecordFieldNumber != 3) {
                        continue;
                    }
                    string foundRef = null;
                    switch (reader.RecordValue) {
                        // dynamic access
                        case "ACCESS":
                            // "file.p" "file.p" line ACCESS [DATA-MEMBER] random.table1 idx_1 WHOLE-INDEX
                            if (reader.MoveToNextRecordField() && reader.RecordFieldNumber == 4) {
                                foundRef = reader.RecordValue;
                                if (foundRef.Equals("DATA-MEMBER") && reader.MoveToNextRecordField() && reader.RecordFieldNumber == 5) {
                                    foundRef = reader.RecordValue;
                                }
                            }
                            break;
                        // dynamic access
                        case "CREATE":
                        case "DELETE":
                        case "UPDATE":
                        case "SEARCH":
                            // "file.p" "file.p" line SEARCH random.table1 idx_1 WHOLE-INDEX
                            if (reader.MoveToNextRecordField() && reader.RecordFieldNumber == 4) {
                                foundRef = reader.RecordValue;
                            }
                            break;
                        // static reference
                        case "REFERENCE":
                            // "file.p" "file.p" line REFERENCE random.table1 
                            if (reader.MoveToNextRecordField() && reader.RecordFieldNumber == 4) {
                                foundRef = reader.RecordValue;
                            }
                            break;
                        // static reference
                        case "NEW-SHR-WORKFILE":
                        case "NEW-SHR-WORKTABLE":
                        case "SHR-WORKFILE":
                        case "SHR-WORKTABLE":
                            // "file.p" "file.p" line SHR-WORKFILE WORKtable2 LIKE random.table1
                            if (reader.MoveToNextRecordField() && reader.RecordFieldNumber == 4 && 
                                reader.MoveToNextRecordField() && reader.RecordFieldNumber == 5 && 
                                reader.MoveToNextRecordField() && reader.RecordFieldNumber == 6) {
                                foundRef = reader.RecordValue;
                            }
                            break;
                        default:
                            continue;
                    }

                    if (!references.Contains(foundRef)) {
                        references.Add(foundRef);
                    }
                }
            }

            return references;
        }

        /// <summary>
        /// Returns the environment variable DLC value
        /// </summary>
        /// <returns></returns>
        public static string GetDlcPathFromEnv() {
            return Environment.GetEnvironmentVariable(UoeConstants.OeDlcEnvVar) ?? throw new UoeDlcNotFoundException($"Can't find the openedge installation directory ({UoeConstants.OeDlcEnvVar}).");
        }

        /// <summary>
        /// Returns the detailed message found in the prohelp folder of dlc corresponding to the given error number
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="errorNumber"></param>
        /// <returns></returns>
        public static ProMsg GetOpenedgeProMessage(string dlcPath, int errorNumber) {
            var messageDir = Path.Combine(dlcPath, "prohelp", "msgdata");
            if (!Directory.Exists(messageDir)) {
                return null;
            }

            var messageFile = Path.Combine(messageDir, $"msg{(errorNumber - 1) / 50 + 1}");
            if (!File.Exists(messageFile)) {
                return null;
            }

            ProMsg outputMessage = null;

            var err = errorNumber.ToString();
            using (var reader = new UoeExportReader(messageFile, Encoding.Default)) {
                while (reader.MoveToNextRecordField()) {
                    if (reader.RecordFieldNumber == 0 && reader.RecordValue == err) {
                        outputMessage = new ProMsg {
                            Number = errorNumber,
                            Text = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty,
                            Description = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty,
                            CategoryFirstLetter = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty,
                            KnowledgeBase = reader.MoveToNextRecordField() ? reader.RecordValueNoQuotes : string.Empty
                        };
                        break;
                    }
                }
            }
            
            return outputMessage;
        }

        /// <summary>
        /// Represents an openedge prosmg
        /// </summary>
        public class ProMsg {
            public int Number { get; set; }
            public string Text { get; set; }
            public string Description { get; set; }
            public string CategoryFirstLetter { get; set; }
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
                    return categories.FirstOrDefault(c => c.StartsWith(CategoryFirstLetter, StringComparison.OrdinalIgnoreCase));
                }
            }

            public override string ToString() {
                var cat = Category;
                return string.Format("{0}{1}{2}", cat != null ? $"({cat}) " : "", Description, KnowledgeBase.Length > 2 ? $" ({KnowledgeBase.StripQuotes()})" : "");
            }
        }
        
        /// <summary>
        /// Returns the openedge version currently installed
        /// </summary>
        /// <remarks>https://knowledgebase.progress.com/articles/Article/P126</remarks>
        public static Version GetProVersionFromDlc(string dlcPath) {
            var versionFilePath = Path.Combine(dlcPath, "version");
            if (File.Exists(versionFilePath)) {
                var matches = new Regex(@"(\d+)\.(\d+)(?:\.(\d+)|([A-Za-z](\d+)))?").Matches(File.ReadAllText(versionFilePath));
                if (matches.Count == 1) {
                    return new Version(int.Parse(matches[0].Groups[1].Value), int.Parse(matches[0].Groups[2].Value), int.Parse(matches[0].Groups[3].Success ? matches[0].Groups[3].Value : matches[0].Groups[5].Success ? matches[0].Groups[5].Value : "0"));
                }
            }

            return null;
        }

        /// <summary>
        /// Returns wether or not the progress version accepts the -nosplash parameter
        /// </summary>
        /// <param name="proVersion"></param>
        /// <returns></returns>
        public static bool CanProVersionUseNoSplashParameter(Version proVersion) => proVersion != null && proVersion.CompareTo(new Version(11, 6, 0)) >= 0;

        /// <summary>
        /// Returns a valid encoding from an openedge codepage name.
        /// </summary>
        /// <param name="codePage"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static bool GetEncodingFromOpenedgeCodePage(string codePage, out Encoding encoding) {
            try {
                if (char.IsDigit(codePage[0]) && codePage.Length == 4) {
                    codePage = $"Windows-{codePage}";
                } else if (codePage.StartsWith("iso", StringComparison.OrdinalIgnoreCase)) {
                    codePage = $"ISO-{codePage.Substring(3).TrimStart('-')}";
                }
                encoding = Encoding.GetEncoding(codePage);
                return true;
            } catch (Exception) {
                encoding = Encoding.Default;
                return false;
            }
        }
        /// <summary>
        /// Returns the openedge name of an encoding.
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static string GetOpenedgeCodePageFromEncoding(Encoding encoding) {
            if (encoding == null) {
                return "undefined";
            }
            var output = encoding.WebName;
            if (output.StartsWith("windows-", StringComparison.OrdinalIgnoreCase)) {
                output = output.Substring(8);
            } else if (output.StartsWith("iso-", StringComparison.OrdinalIgnoreCase)) {
                output = $"iso{output.Substring(4)}";
            }
            return output;
        }
        
        /// <summary>
        /// Returns the codepage used by a progress session for the graphical client.
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <returns></returns>
        public static string GetGuiCodePageFromDlc(string dlcPath) {
            var startupFilePath = Path.Combine(dlcPath, "startup.pf");
            if (File.Exists(startupFilePath)) {
                var matches = new Regex(@"-cpinternal ([\w-]+)(\s|$)").Matches(File.ReadAllText(startupFilePath));
                if (matches.Count > 0) {
                    return matches[0].Groups[1].Value;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Returns the codepage used by a progress session for all file/console I/O.
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <returns></returns>
        public static string GetIoCodePageFromDlc(string dlcPath) {
            var startupFilePath = Path.Combine(dlcPath, "startup.pf");
            Match match = null;
            if (File.Exists(startupFilePath)) {
                var matches = new Regex(@"(-cpterm|-stream|-cpstream) ([\w-]+)(\s|$)").Matches(File.ReadAllText(startupFilePath));
                
                if (matches.Count == 1) {
                    match = matches[0];
                } else if (matches.Count == 2) {
                    match = matches[0];
                    foreach (Match match1 in matches) {
                        if (match1.Groups[1].Success && match1.Groups[1].Value.Equals("-cpterm", StringComparison.OrdinalIgnoreCase)) {
                            match = match1;
                        }
                    }
                }
            }
            return match != null && match.Groups[2].Success ? match.Groups[2].Value : null;
        }

        /// <summary>
        /// Returns the pro executable full path from the dlc path
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="useCharacterModeOfProgress"></param>
        /// <exception cref="UoeExecutionParametersException">invalid mode (gui/char) or path not found</exception>
        /// <returns></returns>
        public static string GetProExecutableFromDlc(string dlcPath, bool useCharacterModeOfProgress = false) {
            string outputPath;
            if (Utils.IsRuntimeWindowsPlatform) {
                outputPath = Path.Combine(dlcPath, "bin", useCharacterModeOfProgress ? "_progres.exe" : "prowin32.exe");
                if (!File.Exists(outputPath)) {
                    if (useCharacterModeOfProgress) {
                        throw new UoeExecutionParametersException($"Could not find the progress executable for character mode in {dlcPath}, check your DLC path or switch to graphical mode; the file searched was {outputPath}.");
                    }

                    outputPath = Path.Combine(dlcPath, "bin", "prowin.exe");
                }
            } else {
                if (!useCharacterModeOfProgress) {
                    throw new UoeExecutionParametersException("Graphical mode unavailable on non windows platform, use the character mode of openedge (_progres).");
                }

                outputPath = Path.Combine(dlcPath, "bin", "_progres");
            }

            if (!File.Exists(outputPath)) {
                throw new UoeExecutionParametersException($"Could not find the progress executable in {dlcPath}, check your DLC path; the file searched was {outputPath}.");
            }

            return outputPath;
        }

        /// <summary>
        /// Reads the propath from an ini file, only returns existing folders or files (.pl)
        /// takes care of relative path and environment variables (%DLC%, $DLC)
        /// </summary>
        /// <param name="iniFile"></param>
        /// <param name="currentDirectory"></param>
        /// <returns></returns>
        public static HashSet<string> GetProPathFromIniFile(string iniFile, string currentDirectory) {
            var uniqueDirList = new HashSet<string>();

            var propath = new IniReader(iniFile).GetValue("PROPATH", "");

            foreach (var path in propath.Split(',', '\n', ';').Select(path => path.Trim()).Where(path => !string.IsNullOrEmpty(path))) {
                try {
                    var thisPath = path.ToCleanPath();

                    // replace environment variables
                    if (thisPath.Contains("%") || thisPath.Contains("$")) {
                        thisPath = Environment.ExpandEnvironmentVariables(thisPath);
                    }

                    // need to take into account relative paths
                    if (!Path.IsPathRooted(thisPath)) {
                        thisPath = Path.GetFullPath(Path.Combine(currentDirectory ?? "", thisPath));
                    }

                    if (!Directory.Exists(thisPath) && !File.Exists(thisPath)) {
                        continue;
                    }

                    if (!uniqueDirList.Contains(thisPath)) {
                        uniqueDirList.Add(thisPath);
                    }
                } catch (Exception) {
                    // ignore bad directories
                }
            }

            return uniqueDirList;
        }

        /// <summary>
        /// Reads a database connection string from a progress parameter file (takes comment into account)
        /// </summary>
        /// <param name="pfPath"></param>
        /// <returns></returns>
        public static string GetConnectionStringFromPfFile(string pfPath) {
            if (!File.Exists(pfPath))
                return string.Empty;

            var connectionString = new StringBuilder();
            Utils.ForEachLine(pfPath, new byte[0], (nb, line) => {
                if (!string.IsNullOrEmpty(line)) {
                    connectionString.Append(" ");
                    connectionString.Append(line);
                }
            });
            connectionString.Append(" ");
            return connectionString.CliCompactWhitespaces().ToString();
        }

        /// <summary>
        /// List all the prolib files in a directory
        /// </summary>
        /// <param name="baseDirectory"></param>
        /// <returns></returns>
        public static List<string> ListProlibFilesInDirectory(string baseDirectory) {
            return Directory.EnumerateFiles(baseDirectory, $"*{UoeConstants.ExtProlibFile}", SearchOption.TopDirectoryOnly).ToList();
        }

        /// <summary>
        /// Returns the default propath used by a progress session, depending on the mode gui/tty
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="useCharacterMode"></param>
        /// <returns></returns>
        public static List<string> GetProgressSessionDefaultPropath(string dlcPath, bool useCharacterMode) {
            var path = Path.Combine(dlcPath, useCharacterMode ? "tty" : "gui");
            if (!Directory.Exists(path)) {
                return null;
            }

            var output = new List<string> {
                path
            };
            output.AddRange(ListProlibFilesInDirectory(path));
            output.Add(dlcPath.ToCleanPath());
            output.Add(Path.Combine(dlcPath, "bin"));
            return output;
        }
    }
}