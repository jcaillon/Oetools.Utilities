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
        /// Will contain the list of table/tCRC referenced by the compiled rcode
        /// uses RCODE-INFO:TABLE-LIST to get a list of referenced TABLES in the file,
        /// this list of tables does not include referenced table in statements like :
        /// - DEF VAR efzef LIKE DB.TABLE
        /// - DEF TEMP-TABLE zefezf LIKE DB.TABLE
        /// and so on...
        /// If a table listed here is modified, the source file should be recompiled or, at runtime, you would have a bad CRC error
        /// Note : when you refer a sequence, the TABLE-LIST will have an entry like : DATABASENAME._Sequence
        /// </summary>
        public string CompilationRcodeTableListFilePath { get; set; }

        public string CompilationRcodeFilePath { get; set; }
        
        public bool IsAnalysisMode { get; set; }
        
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
        /// represent the tables or sequences that were referenced in a given .r code file and thus needed to compile
        /// also, if one reference changes, the file should be recompiled
        /// it is list of DATABASENAME.TABLENAME or DATABASENAME.SEQUENCENAME, you should probably verify
        /// that those references do exist afterward and also get the TABLE CRC value
        /// </summary>
        public List<string> RequiredDatabaseReferences { get; private set; }
       
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

        private bool _compilationResultsRead;
        
        public void ReadCompilationResults() {
            if (_compilationResultsRead) {
                return;
            }

            _compilationResultsRead = true;
            
            // make sure that the expected generated files are actually generated
            AddWarningIfFileDefinedButDoesNotExist(CompilationListingFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationXrefFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationXmlXrefFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationDebugListFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationPreprocessedFilePath);
            
            CorrectRcodePathForClassFiles();

            // read compilation errors/warning for this file
            ReadCompilationErrors();

            if (IsAnalysisMode) {
                AddWarningIfFileDefinedButDoesNotExist(CompilationFileIdLogFilePath);
                AddWarningIfFileDefinedButDoesNotExist(CompilationRcodeTableListFilePath);
                ComputeDatabaseReferences();
                ComputeReferencedFiles();
            }

            CompiledCorrectly = File.Exists(CompilationRcodeFilePath) && (CompilationErrors == null || CompilationErrors.Count == 0);
        }

        private void AddWarningIfFileDefinedButDoesNotExist(string path) {
            if (!string.IsNullOrEmpty(path) && !File.Exists(path)) {
                (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(new CompilationError {
                    SourcePath = SourceFilePath,
                    Column = 1,
                    Line = 1,
                    Level = CompilationErrorLevel.Warning,
                    ErrorNumber = 0,
                    Message = $"{path} has not been generated"
                });
            }
        }

        private void ReadCompilationErrors() {
            if (!string.IsNullOrEmpty(CompilationErrorsFilePath) && File.Exists(CompilationErrorsFilePath)) {
                Utils.ForEachLine(CompilationErrorsFilePath, null, (i, line) => {
                    var fields = line.Split('\t');
                    if (fields.Length == 7) {
                        var error = new CompilationError {
                            SourcePath = fields[1].Equals(CompiledFilePath) ? SourceFilePath : fields[1],
                            Line = Math.Max(0, (int) fields[3].ConvertFromStr(typeof(int))),
                            Column = Math.Max(0, (int) fields[4].ConvertFromStr(typeof(int))),
                            ErrorNumber = Math.Max(0, (int) fields[5].ConvertFromStr(typeof(int)))
                        };

                        if (!Enum.TryParse(fields[2], true, out CompilationErrorLevel compilationErrorLevel))
                            compilationErrorLevel = CompilationErrorLevel.Error;
                        error.Level = compilationErrorLevel;

                        error.Message = fields[6].ProUnescapeString().Replace(CompiledFilePath, SourceFilePath).Trim();

                        (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(error);
                    }
                });
            }
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
        private void ComputeReferencedFiles() {

            if (string.IsNullOrEmpty(CompilationFileIdLogFilePath)) {
                return;
            }
            
            if (File.Exists(CompilationFileIdLogFilePath)) {
                var compiledSourcePathBaseFileName = Path.GetFileName(SourceFilePath);
                var references = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                Utils.ForEachLine(CompilationFileIdLogFilePath, null, (i, line) => {
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
                            !Path.GetFileName(newFile).Equals(compiledSourcePathBaseFileName)) {
                            references.Add(newFile);
                        }
                    } catch (Exception e) {
                        // wrong line format
                        (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(new CompilationError {
                            SourcePath = SourceFilePath,
                            Column = 1,
                            Line = 1,
                            Level = CompilationErrorLevel.Warning,
                            ErrorNumber = 0,
                            Message = $"Error catched when analyzing the FILEID log : {e.ToString()}"
                        });
                    }
                }, Encoding.Default);
                
                RequiredFiles = references.ToList();
            }
        }

        private void ComputeDatabaseReferences() {
            
            // for reference, below are 
            
            // DEFINE VARIABLE li_i AS INTEGER NO-UNDO.
            // /* ACCESS */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 4 ACCESS random.sequence1 SEQUENCE*/
            // ASSIGN li_i = CURRENT-VALUE(sequence1).
            // /* UPDATE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 7 UPDATE random.sequence1 SEQUENCE*/
            // ASSIGN CURRENT-VALUE(sequence1) = 1.
            // /* SEARCH */
            // /* "C:\folder space\file.p" "C:\folder space\file.p" 10 SEARCH random.table1 idx_1 WHOLE-INDEX */
            // FIND FIRST table1.
            // /* ACCESS */
            // /* SEARCH */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 16 ACCESS random.table1 field1 */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 16 SEARCH random.table1 idx_1 WHOLE-INDEX*/
            // FOR EACH table1 BY table1.field1:
            // END.
            // /* CREATE */
            // /* "C:\folder space\file.p" "C:\folder space\file.p" 20 CREATE random.table1  */
            // CREATE table1.
            // /* UPDATE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 24 ACCESS random.table1 field1 */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 24 UPDATE random.table1 field1*/
            // ASSIGN table1.field1 = "".
            // /* DELETE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 27 DELETE random.table1 */
            // DELETE table1.
            // /* NEW-SHR-WORKFILE */
            // /* NEW-SHR-WORKTABLE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 32 REFERENCE random.table1 */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 32 NEW-SHR-WORKFILE WORKtable1 LIKE random.table1*/
            // DEFINE NEW SHARED WORKFILE wftable1 NO-UNDO LIKE table1.
            // DEFINE NEW SHARED WORK-TABLE wttable1 NO-UNDO LIKE table1. /* same thing as WORKFILE */
            // /* SHR-WORKFILE */
            // /* SHR-WORKTABLE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 38 REFERENCE random.table1 */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 38 SHR-WORKFILE WORKtable2 LIKE random.table1*/
            // DEFINE SHARED WORKFILE wftable2 NO-UNDO LIKE table1.
            // DEFINE SHARED WORK-TABLE wttable2 NO-UNDO LIKE table1. /* same thing as WORKFILE */
            // /* REFERENCE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 42 REFERENCE random.table1 field1 */
            // DEFINE VARIABLE lc_ LIKE table1.field1 NO-UNDO.
            // /* REFERENCE */
            // /*"C:\folder space\file.p" "C:\folder space\file.p" 45 REFERENCE random.table1 */
            // DEFINE TEMP-TABLE tt1 LIKE table1.
            
            if (string.IsNullOrEmpty(CompilationXrefFilePath) && string.IsNullOrEmpty(CompilationRcodeTableListFilePath)) {
                return;
            }
            
            var references = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            
            // read RCodeTableReferenced
            if (File.Exists(CompilationRcodeTableListFilePath)) {
                Utils.ForEachLine(CompilationRcodeTableListFilePath, null, (i, line) => {
                    var split = line.Split(' ');
                    if (split.Length >= 1) {
                        var qualifiedName = split[0].Trim();
                        if (!references.Contains(qualifiedName)) {
                            references.Add(qualifiedName);
                        }
                    }
                }, Encoding.Default);
            }

            if (File.Exists(CompilationXrefFilePath)) {
                ProUtilities.ReadOpenedgeUnformattedExportFile(CompilationXrefFilePath, record => {
                    if (record.Count < 5) {
                        return true;
                    }

                    string foundRef = null;
                    switch (record[3]) {
                        // dynamic access
                        case "ACCESS":
                            // "file.p" "file.p" line ACCESS [DATA-MEMBER] random.table1 idx_1 WHOLE-INDEX
                            foundRef = record[4];
                            if (foundRef.Equals("DATA-MEMBER") && record.Count >= 5) {
                                foundRef = record[5];
                            }

                            break;
                        // dynamic access
                        case "CREATE":
                        case "DELETE":
                        case "UPDATE":
                        case "SEARCH":
                            // "file.p" "file.p" line SEARCH random.table1 idx_1 WHOLE-INDEX
                            foundRef = record[4];
                            break;
                        // static reference
                        case "REFERENCE":
                            // "file.p" "file.p" line REFERENCE random.table1 
                            foundRef = record[4];
                            break;
                        // static reference
                        case "NEW-SHR-WORKFILE":
                        case "NEW-SHR-WORKTABLE":
                        case "SHR-WORKFILE":
                        case "SHR-WORKTABLE":
                            // "file.p" "file.p" line SHR-WORKFILE WORKtable2 LIKE random.table1
                            if (record.Count >= 6) {
                                foundRef = record[6];
                            }

                            break;
                        default:
                            return true;
                    }

                    if (!string.IsNullOrEmpty(foundRef) && foundRef.IndexOf('.') > 0 && !references.Contains(foundRef)) {
                        // make sure it's actually a table or sequence
                        references.Add(foundRef);
                    }

                    return true;
                }, out List<Exception> le);
                if (le != null) {
                    foreach (var exception in le) {
                        (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(new CompilationError {
                            SourcePath = SourceFilePath,
                            Column = 1,
                            Line = 1,
                            Level = CompilationErrorLevel.Warning,
                            ErrorNumber = 0,
                            Message = $"Error catched when analyzing the XREF file : {exception}"
                        });
                    }
                }
            }

            RequiredDatabaseReferences = references.ToList();
        }
    }

}