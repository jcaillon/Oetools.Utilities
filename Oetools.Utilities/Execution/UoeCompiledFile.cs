#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeCompiledFile.cs) is part of Oetools.Utilities.
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
using DotUtilities;
using DotUtilities.Extensions;
using Oetools.Utilities.Openedge.Database;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    ///     This class represents a file thas been compiled
    /// </summary>
    public class UoeCompiledFile : IPathListItem {
        /// <summary>
        ///     The path to the source that has been compiled
        /// </summary>
        public string Path { get; }

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
        /// This temporary file is actually a log with only FileId activated just before the compilation
        /// and deactivated just after; this allows us to know which file were used to compile the source
        /// </summary>
        /// <remarks>
        /// Why don't we use the INCLUDE lines in the .xref file?
        /// because it is not directly a file path,
        /// it the content of what is between {an include}, which means it is a relative path (from PROPATH)
        /// and it can contains all the parameters... It easier to use this method
        /// also because we would need to analyse CLASS lines to know if the .cls depends on others...
        /// Lot of work, FILEID is the easy road
        /// </remarks>
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

        public bool CompiledWithWarnings { get; private set; }

        /// <summary>
        /// If the compiled file is a class file, it will represent the class namespace as a path
        /// For instance, compiling random.cool.Class1 will return random\cool (or random/cool depending on the platform)
        /// </summary>
        public string ClassNamespacePath { get; private set; }

        /// <summary>
        ///     List of errors
        /// </summary>
        public List<AUoeCompilationProblem> CompilationProblems { get; set; }

        /// <summary>
        ///     represents the source file (i.e. includes) used to generate a given .r code file
        /// </summary>
        public HashSet<string> RequiredFiles { get; private set; }

        /// <summary>
        /// represent the tables or sequences that were referenced in a given .r code file and thus needed to compile
        /// also, if one reference changes, the file should be recompiled
        /// it is list of DATABASENAME.TABLENAME or DATABASENAME.SEQUENCENAME, you should probably verify
        /// that those references do exist afterward and also get the TABLE CRC value
        /// </summary>
        public List<UoeDatabaseReference> RequiredDatabaseReferences { get; private set; }

        /// <summary>
        ///     Returns the base file name (set in constructor)
        /// </summary>
        public string BaseFileName { get; }


        /// <summary>
        ///     Constructor
        /// </summary>
        public UoeCompiledFile(UoeFileToCompile fileToCompile) {
            Path = fileToCompile.Path;
            CompiledFilePath = fileToCompile.CompiledPath;
            BaseFileName = System.IO.Path.GetFileNameWithoutExtension(Path);
        }

        private bool _compilationResultsRead;

        public void ReadCompilationResults(Encoding enc, string currentDirectory) {
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
            ReadCompilationErrors(enc);

            if (IsAnalysisMode) {
                AddWarningIfFileDefinedButDoesNotExist(CompilationFileIdLogFilePath);
                ComputeReferencedFiles(enc, currentDirectory);
            }

            var rcodeExists = File.Exists(CompilationRcodeFilePath);
            CompiledCorrectly = rcodeExists && (CompilationProblems == null || CompilationProblems.Count == 0);
            CompiledWithWarnings = !CompiledCorrectly && rcodeExists && (CompilationProblems == null || CompilationProblems.All(e => e is UoeCompilationWarning));
        }

        /// <summary>
        /// Read the table referenced in the source file using either the xref file or the RCODE-INFO:TABLE-LIST,
        /// make sure to also get the CRC value for each table
        /// </summary>
        /// <param name="ienv"></param>
        /// <param name="analysisModeSimplifiedDatabaseReferences"></param>
        public void ComputeRequiredDatabaseReferences(AUoeExecutionEnv ienv, bool analysisModeSimplifiedDatabaseReferences) {
            if (!IsAnalysisMode || string.IsNullOrEmpty(CompilationXrefFilePath) && string.IsNullOrEmpty(CompilationRcodeTableListFilePath)) {
                return;
            }
            AddWarningIfFileDefinedButDoesNotExist(CompilationRcodeTableListFilePath);

            RequiredDatabaseReferences = new List<UoeDatabaseReference>();

            // read from xref (we need the table CRC from the environment)
            if (ienv is UoeExecutionEnv env && !analysisModeSimplifiedDatabaseReferences) {
                if (File.Exists(CompilationXrefFilePath)) {
                    foreach (var dbRef in UoeUtilities.GetDatabaseReferencesFromXrefFile(CompilationXrefFilePath, ienv.IoEncoding)) {
                        if (env.TablesCrc.ContainsKey(dbRef)) {
                            RequiredDatabaseReferences.Add(new UoeDatabaseReferenceTable {
                                QualifiedName = dbRef,
                                Crc = env.TablesCrc[dbRef]
                            });
                        } else if (env.Sequences.Contains(dbRef)) {
                            RequiredDatabaseReferences.Add(new UoeDatabaseReferenceSequence {
                                QualifiedName = dbRef
                            });
                        }
                    }
                }
            }

            // read from rcode-info:table-list
            if (File.Exists(CompilationRcodeTableListFilePath)) {
                Utils.ForEachLine(CompilationRcodeTableListFilePath, null, (i, line) => {
                    var split = line.Split('\t');
                    if (split.Length >= 1) {
                        var qualifiedName = split[0].Trim();
                        if (!RequiredDatabaseReferences.Exists(r => r.QualifiedName.EqualsCi(qualifiedName))) {
                            RequiredDatabaseReferences.Add(new UoeDatabaseReferenceTable {
                                QualifiedName = qualifiedName,
                                Crc = split[1].Trim()
                            });
                        }
                    }
                }, ienv.IoEncoding);
            }
        }

        private void AddWarningIfFileDefinedButDoesNotExist(string path) {
            if (!string.IsNullOrEmpty(path) && !File.Exists(path)) {
                (CompilationProblems ?? (CompilationProblems = new List<AUoeCompilationProblem>())).Add(new UoeCompilationWarning {
                    FilePath = Path,
                    Column = 1,
                    Line = 1,
                    ErrorNumber = 0,
                    Message = $"{path} has not been generated."
                });
            }
        }

        private void ReadCompilationErrors(Encoding enc) {
            if (!string.IsNullOrEmpty(CompilationErrorsFilePath) && File.Exists(CompilationErrorsFilePath)) {
                Utils.ForEachLine(CompilationErrorsFilePath, null, (i, line) => {
                    var fields = line.Split('\t');
                    if (fields.Length == 7) {
                        if (!Enum.TryParse(fields[2], true, out CompilationErrorLevel compilationErrorLevel))
                            compilationErrorLevel = CompilationErrorLevel.Error;
                        var problem = AUoeCompilationProblem.New(compilationErrorLevel);
                        problem.FilePath = fields[1].Equals(CompiledFilePath) ? Path : fields[1];
                        problem.Line = Math.Max(1, (int) fields[3].ConvertFromStr(typeof(int)));
                        problem.Column = Math.Max(1, (int) fields[4].ConvertFromStr(typeof(int)));
                        problem.ErrorNumber = Math.Max(0, (int) fields[5].ConvertFromStr(typeof(int)));
                        problem.Message = fields[6].ProUnescapeSpecialChar().Replace(CompiledFilePath, Path).Trim();
                        (CompilationProblems ?? (CompilationProblems = new List<AUoeCompilationProblem>())).Add(problem);
                    }
                }, enc);
            }
        }

        private void CorrectRcodePathForClassFiles() {

            // this only concerns cls files
            if (Path.EndsWith(UoeConstants.ExtCls, StringComparison.OrdinalIgnoreCase)) {
                // Handle the case of .cls files, for which several .r code are compiled
                // if the file we compiled implements/inherits from another class, there is more than 1 *.r file generated.
                // Moreover, they are generated in their respective package folders

                // for each *.r file in the compilation output directory
                foreach (var rCodeFilePath in Directory.EnumerateFiles(CompilationOutputDirectory, $"*{UoeConstants.ExtR}", SearchOption.AllDirectories)) {
                    // if this is actually the .cls file we want to compile, the .r file isn't necessary directly in the compilation dir like we expect,
                    // it can be in folders corresponding to the package of the class
                    if (BaseFileName.Equals(System.IO.Path.GetFileNameWithoutExtension(rCodeFilePath))) {
                        // correct .r path
                        CompilationRcodeFilePath = rCodeFilePath;
                        ClassNamespacePath = System.IO.Path.GetDirectoryName(rCodeFilePath)?.Replace(CompilationOutputDirectory, "").Trim('\\', '/');
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the files that were necessary to compile this file
        /// </summary>
        private void ComputeReferencedFiles(Encoding enc, string currentDirectory) {

            if (string.IsNullOrEmpty(CompilationFileIdLogFilePath)) {
                return;
            }

            if (File.Exists(CompilationFileIdLogFilePath)) {
                RequiredFiles = UoeUtilities.GetReferencedFilesFromFileIdLog(CompilationFileIdLogFilePath, enc, currentDirectory);

                RequiredFiles.RemoveWhere(f =>
                    f.PathEquals(CompiledFilePath) ||
                    f.EndsWith(".r", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".pl", StringComparison.OrdinalIgnoreCase) ||
                    !String.IsNullOrEmpty(CompilationXrefFilePath) && f.PathEquals(CompilationXrefFilePath));

            }
        }

    }

}
