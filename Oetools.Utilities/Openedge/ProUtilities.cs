// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProUtilities.cs) is part of Oetools.Utilities.
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

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Openedge {
    
    public static class ProUtilities {

        public static List<DatabaseReference> GetReferences(CompiledFile compiledFile, List<TableCrc> tables) {
            // TODO : this
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Returns the environment variable DLC value
        /// </summary>
        /// <returns></returns>
        public static string GetDlcPathFromEnv() {
            return Environment.GetEnvironmentVariable("dlc");
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
            ReadOpenedgeUnformattedExportFile(messageFile, record => {
                if (record.Count >= 5) {
                    outputMessage = new ProMsg {
                        Number = errorNumber,
                        Text = record[1].StripQuotes(),
                        Description = record[2].StripQuotes(),
                        Category = record[3].StripQuotes(),
                        KnowledgeBase = record[4].StripQuotes()
                    };
                }
                return false;
            }, out _, (lineNumber, line) => line.StartsWith($"{errorNumber} "));

            return outputMessage;
        }

        public class ProMsg {
            public int Number { get; set; }
            public string Text { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public string KnowledgeBase { get; set; }

            public override string ToString() {
                var categories = new List<string> {
                    "Compiler", "Database", "Index", "Miscellaneous", "Operating System", "Program/Execution", "Syntax"
                };
                var cat = categories.FirstOrDefault(c => c.StartsWith(Category, StringComparison.CurrentCultureIgnoreCase));
                return string.Format("{0}{1}{2}", cat != null ? $"({cat}) " : "", Description, KnowledgeBase.Length > 2 ? $" ({KnowledgeBase.StripQuotes()})" : "");
            }
        }

        /// <summary>
        /// Read records in an openedge .d file
        /// Each line is a record with multiple fields separated by a single space, you can use spaces in a field by double quoting the field
        /// and you can use double quotes by doubling them. A quoted field can extend on multiple lines.
        /// This method doesn't expect all the records to have the same number of fields, even less the same type
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="recordHandler"></param>
        /// <param name="catchedExceptions"></param>
        /// <param name="filterLinePredicate"></param>
        /// <param name="encoding"></param>
        /// <exception cref="Exception"></exception>
        public static void ReadOpenedgeUnformattedExportFile(string filePath, Func<List<string>, bool> recordHandler, out List<Exception> catchedExceptions, Func<int, string, bool> filterLinePredicate = null, Encoding encoding = null) {
            if (encoding == null) {
                encoding = Encoding.Default;
            }
            catchedExceptions = null;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                using (var reader = new StreamReader(fileStream, encoding)) {
                    var i = -1;
                    string line;
                    var record = new List<string>();
                    bool inStringField = false;
                    
                    while ((line = reader.ReadLine()) != null) {
                        try {
                            i++;
                            if (!inStringField && filterLinePredicate != null && !filterLinePredicate(i, line)) {
                                continue;
                            }
                            if (line.Length > 0) {
                                int fieldBegin = 0;
                                do {
                                    int fieldEnd;
                                    if (line[fieldBegin] == '"' || inStringField) {
                                        fieldEnd = fieldBegin + (inStringField ? 0 : 1) - 1;
                                        bool firstLoop = true;
                                        do {
                                            if (!firstLoop && line[fieldEnd + 1] == '"') {
                                                fieldEnd++;
                                            }
                                            firstLoop = false;
                                            fieldEnd = line.IndexOf('"', fieldEnd + 1);
                                            if (fieldEnd < 0) {
                                                fieldEnd = line.Length - 1;
                                            }
                                        } while (fieldEnd < line.Length - 1 && line[fieldEnd + 1] == '"');
                                        var currentRecordValue = line.Substring(fieldBegin, fieldEnd - fieldBegin + 1);
                                        if (currentRecordValue.Length > 2) {
                                            currentRecordValue = currentRecordValue.Replace("\"\"", "\"");
                                        }
                                        if (inStringField) {
                                            record[record.Count - 1] = $"{record.Last()}{Environment.NewLine}{currentRecordValue}";
                                        } else {
                                            record.Add(currentRecordValue);
                                        }
                                        inStringField = line[fieldEnd] != '"';
        
                                    } else {
                                        fieldEnd = line.IndexOf(' ', fieldBegin + 1) - 1;
                                        if (fieldEnd < 0) {
                                            fieldEnd = line.Length - 1;
                                        }
                                        var fieldLength = fieldEnd - fieldBegin + 1;
                                        if (fieldLength == 0) {
                                            (catchedExceptions ?? (catchedExceptions = new List<Exception>())).Add(new Exception($"Bad line format (line {i}), consecutive spaces or line beggining by space"));
                                            
                                        }
                                        record.Add(line.Substring(fieldBegin, fieldLength));
                                    }
                                    fieldBegin = fieldEnd + 2; // next char + skip the space = 2
                                } while (fieldBegin <= line.Length - 1);

                                if (!inStringField) {
                                    if (!recordHandler(record)) {
                                        // stop reading
                                        break;
                                    }
                                    record.Clear();
                                }
                            }
                        } catch (Exception e) {
                            (catchedExceptions ?? (catchedExceptions = new List<Exception>())).Add(new Exception($"Unexpected error reading line {i} : {e}"));
                        }
                    }
                }
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
        /// Returns the pro executable full path from the dlc path
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="useCharacterModeOfProgress"></param>
        /// <exception cref="ExecutionParametersException">invalid mode (gui/char) or path not found</exception>
        /// <returns></returns>
        public static string GetProExecutableFromDlc(string dlcPath, bool useCharacterModeOfProgress = false) {
            string outputPath;
            if (Utils.IsRuntimeWindowsPlatform) {
                outputPath = Path.Combine(dlcPath, "bin", useCharacterModeOfProgress ? "_progres.exe" : "prowin32.exe");
                if (!File.Exists(outputPath)) {
                    if (useCharacterModeOfProgress) {
                        throw new ExecutionParametersException($"Could not find the progress executable for character mode in {dlcPath}, check your DLC path or switch to graphical mode; the file searched was {outputPath}");
                    }
                    outputPath = Path.Combine(dlcPath, "bin", "prowin.exe");
                }
            } else {
                if (!useCharacterModeOfProgress) {
                    throw new ExecutionParametersException("Graphical mode unavailable on non windows platform, use the character mode of openedge (_progres)");
                }

                outputPath = Path.Combine(dlcPath, "bin", "_progres");
            }

            if (!File.Exists(outputPath)) {
                throw new ExecutionParametersException($"Could not find the progress executable in {dlcPath}, check your DLC path; the file searched was {outputPath}");
            }

            return outputPath;
        }

        /// <summary>
        /// Reads the propath from an ini file
        /// </summary>
        /// <param name="iniFile"></param>
        /// <param name="currentDirectory"></param>
        /// <returns></returns>
        public static HashSet<string> GetProPathFromIniFile(string iniFile, string currentDirectory) {
            var uniqueDirList = new HashSet<string>();

            var propath = new IniReader(iniFile).GetValue("PROPATH", "");

            foreach (var path in propath.Split(',', '\n', ';').Select(path => path.Trim()).Where(path => !string.IsNullOrEmpty(path))) {
                try {
                    var thisPath = path;

                    // replace environment variables
                    if (thisPath.Contains("%") || thisPath.Contains("$")) {
                        thisPath = Environment.ExpandEnvironmentVariables(thisPath);
                    }

                    // need to take into account relative paths
                    if (!Path.IsPathRooted(thisPath)) {
                        thisPath = Path.GetFullPath(Path.Combine(currentDirectory, thisPath));
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
            return Utils.EnumerateFiles(baseDirectory, $"*{OeConstants.ExtProlibFile}", SearchOption.TopDirectoryOnly).ToList();
        }

        /// <summary>
        /// Returns the default propath used by a progress session, depending on the mode gui/tty
        /// </summary>
        /// <param name="dlcPath"></param>
        /// <param name="useCharacterMode"></param>
        /// <returns></returns>
        public static List<string> ReturnProgressSessionDefaultPropath(string dlcPath, bool useCharacterMode) {
            var path = Path.Combine(dlcPath, useCharacterMode ? "tty" : "gui");
            if (!Directory.Exists(path)) {
                return null;
            }        
            var output = new List<string> { path };
            output.AddRange(ListProlibFilesInDirectory(path));
            output.Add(dlcPath);
            output.Add(Path.Combine(dlcPath, "bin"));
            return output;
        }
    }
}