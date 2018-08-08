// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileToCompile.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    ///     This class represents a file thas been compiled
    /// </summary>
    public class CompiledFile {
        /// <summary>
        ///     The path to the source that has been compiled
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// The path of the file that actually needs to be compiled
        /// (can be different from sourcepath if we edited it without saving it for instance)
        /// </summary>
        public string CompiledFilePath { get; }

        public string CompilationOutputDirectory { get; set; }

        public string CompilationErrorsFilePath { get; set; }
        public string CompilationListingFilePath { get; set; }
        public string CompilationXrefFilePath { get; set; }
        public string CompilationXmlXrefFilePath { get; set; }
        public string CompilationDebugListFilePath { get; set; }
        public string CompilationPreprocessedFilePath { get; set; }

        /// <summary>
        ///     This temporary file is actually a log with only FileId activated just before the compilation
        ///     and deactivated just after; this allows us to know which file were used to compile the source
        /// </summary>
        public string CompilationFileIdLogFilePath { get; set; }

        /// <summary>
        ///     Temporary file that list the "table\tCRC" for each referenced table in the output .r
        /// </summary>
        public string CompilationReferencedTablesFilePath { get; set; }

        public string CompilationRcodeFilePath { get; set; }
        
        public bool CompiledCorrectly { get; private set; }
        
        /// <summary>
        ///     List of errors
        /// </summary>
        public List<CompilationError> CompilationErrors { get; set; }

        /// <summary>
        ///     represents the source file (i.e. includes) used to generate a given .r code file
        /// </summary>
        public List<string> RequiredFiles { get; private set; }

        /// <summary>
        ///     represent the tables that were referenced in a given .r code file
        /// </summary>
        public List<TableCrc> RequiredTables { get; private set; }

        /// <summary>
        ///     Returns the base file name (set in constructor)
        /// </summary>
        public string BaseFileName { get; }

        /// <summary>
        ///     Constructor
        /// </summary>
        public CompiledFile(FileToCompile fileToCompile) {
            SourceFilePath = fileToCompile.SourcePath;
            CompiledFilePath = fileToCompile.CompiledPath;
            BaseFileName = Path.GetFileNameWithoutExtension(SourceFilePath);
        }


        public void ReadCompilationResults() {
            CorrectRcodePathForClassFiles();

            // read compilation errors/warning for this file
            if (!string.IsNullOrEmpty(CompilationErrorsFilePath) && File.Exists(CompilationErrorsFilePath)) {
                Utils.ForEachLine(CompilationErrorsFilePath, new byte[0], (i, line) => {
                    var fields = line.Split('\t');
                    if (fields.Length == 7) {
                        var error = new CompilationError {
                            SourcePath = fields[1],
                            Line = Math.Max(0, (int) fields[3].ConvertFromStr(typeof(int)) - 1),
                            Column = Math.Max(0, (int) fields[4].ConvertFromStr(typeof(int)) - 1),
                            ErrorNumber = Math.Max(0, (int) fields[5].ConvertFromStr(typeof(int)) - 1)
                        };

                        if (!Enum.TryParse(fields[2], true, out CompilationErrorLevel compilationErrorLevel))
                            compilationErrorLevel = CompilationErrorLevel.Error;
                        error.Level = compilationErrorLevel;

                        error.Message = fields[6].ProUnescapeString().Replace(fields[0], BaseFileName).Replace(CompiledFilePath, SourceFilePath).Trim();

                        (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(error);
                    }
                });
            }

            CompiledCorrectly = File.Exists(CompilationRcodeFilePath) && CompilationErrors == null;
        }
        
        private void CorrectRcodePathForClassFiles() {
            
            // this only concerns cls files
            if (SourceFilePath.EndsWith(OeConstants.ExtCls, StringComparison.CurrentCultureIgnoreCase)) {
                // Handle the case of .cls files, for which several .r code are compiled
                // if the file we compiled implements/inherits from another class, there is more than 1 *.r file generated.
                // Moreover, they are generated in their respective package folders

                // for each *.r file in the compilation output directory
                foreach (var rCodeFilePath in Directory.EnumerateFiles(CompilationOutputDirectory, $"*{OeConstants.ExtR}", SearchOption.AllDirectories)) {
                    // if this is actually the .cls file we want to compile, the .r file isn't necessary directly in the compilation dir like we expect,
                    // it can be in folders corresponding to the package of the class
                    if (BaseFileName.Equals(Path.GetFileNameWithoutExtension(rCodeFilePath))) {
                        // correct .r path
                        CompilationRcodeFilePath = rCodeFilePath;
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     File the references used to compile this file (include and tables)
        /// </summary>
        public void ReadAnalysisResults() {
            
            // read RCodeTableReferenced
            if (!string.IsNullOrEmpty(CompilationReferencedTablesFilePath) && File.Exists(CompilationReferencedTablesFilePath)) {
                RequiredTables = new List<TableCrc>();
                Utils.ForEachLine(CompilationReferencedTablesFilePath, new byte[0], (i, line) => {
                    var split = line.Split('\t');
                    if (split.Length == 2) {
                        var crc = split[1].Trim();
                        var qualifiedName = split[0].Trim();
                        if (!crc.Equals("0"))
                            if (!RequiredTables.Exists(tableCrc => tableCrc.QualifiedTableName.EqualsCi(qualifiedName)))
                                RequiredTables.Add(new TableCrc {
                                    QualifiedTableName = qualifiedName,
                                    Crc = crc
                                });
                    }
                }, Encoding.Default);
            }

            // read RCodeSourceFileUsed
            if (!string.IsNullOrEmpty(CompilationFileIdLogFilePath) && File.Exists(CompilationFileIdLogFilePath)) {
                var compiledSourcePathBaseFileName = Path.GetFileName(SourceFilePath);
                var references = new HashSet<string>();
                Utils.ForEachLine(CompilationFileIdLogFilePath, new byte[0], (i, line) => {
                    try {
                        // we want to read this kind of line :
                        // [17/04/09@16:44:14.372+0200] P-009532 T-007832 2 4GL FILEID         Open E:\Common\CommonObj.cls ID=33
                        // skip until the 5th space
                        var idx = 0;
                        var nbFoundSpace = 0;
                        do {
                            if (line[idx] == ' ') {
                                nbFoundSpace++;
                                if (nbFoundSpace == 5)
                                    break;
                            }
                            idx++;
                        } while (idx < line.Length);

                        idx++;
                        // the next thing we read should be FILEID
                        if (!line.Substring(idx, 6).Equals("FILEID"))
                            return;
                        idx += 6;
                        // skip all whitespace
                        while (idx < line.Length) {
                            if (line[idx] != ' ')
                                break;
                            idx++;
                        }

                        // now we should read Open
                        if (idx > line.Length - 1 || !line.Substring(idx, 5).Equals("Open "))
                            return;
                        idx += 5;
                        // find the last index of a white space
                        var lastIdx = line.Length - 1;
                        do {
                            if (line[lastIdx] == ' ')
                                break;
                            lastIdx--;
                        } while (lastIdx >= 0);

                        var newFile = line.Substring(idx, lastIdx - idx);

                        if (!references.Contains(newFile) && 
                            !newFile.EndsWith(".r", StringComparison.CurrentCultureIgnoreCase) && 
                            !newFile.EndsWith(".pl", StringComparison.CurrentCultureIgnoreCase) && 
                            !newFile.StartsWith(CompilationOutputDirectory, StringComparison.CurrentCultureIgnoreCase) && 
                            !Path.GetFileName(newFile).Equals(compiledSourcePathBaseFileName))
                            references.Add(newFile);
                    } catch (Exception) {
                        // wrong line format
                    }
                }, Encoding.Default);
                RequiredFiles = references.ToList();
            }
        }
    }

}