// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProExecutionHandleCompilation.cs) is part of csdeployer.
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
using System.Threading.Tasks;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {

    public abstract class OeExecutionHandleCompilation : OeExecution {

        /// <summary>
        /// Number of files already treated
        /// </summary>
        public int NbFilesTreated => unchecked((int) (File.Exists(_progressionFilePath) ? new FileInfo(_progressionFilePath).Length : 0));

        /// <summary>
        ///     The action to execute at the end of the compilation if it went well. It sends :
        ///     - the list of all the files that needed to be compiled,
        ///     - the errors for each file compiled (if any)
        ///     - the list of all the deployments needed for the files compiled (move the .r but also .dbg and so on...)
        /// </summary>
        public event Action<OeExecutionHandleCompilation, List<CompiledFile>> OnCompilationOk;
        
        /// <summary>
        ///     When true, we activate the log just before compiling with FileId active + we generate a file that list referenced
        ///     table in the .r
        /// </summary>
        public bool IsAnalysisMode { get; set; }

        public bool CompileWithDebugList { get; set; }
        public bool CompileWithXref { get; set; }
        public bool CompileWithListing { get; set; }
        public bool CompileUseXmlXref { get; set; }

        /// <summary>
        /// List of the files to compile / run / prolint
        /// </summary>
        public List<FileToCompile> FilesToCompile { get; set; }

        public List<CompiledFile> CompiledFiles { get; } = new List<CompiledFile>();

        /// <summary>
        /// Path to the file containing the COMPILE output
        /// </summary>
        private string _compilationLog;
        
        private string _filesListPath;

        /// <summary>
        /// Path to a file used to determine the progression of a compilation (useful when compiling multiple programs)
        /// 1 byte = 1 file treated
        /// </summary>
        private string _progressionFilePath;

        public OeExecutionHandleCompilation(IEnvExecution env) : base(env) {
            _filesListPath = Path.Combine(_tempDir, "files.list");
            _progressionFilePath = Path.Combine(_tempDir, "compile.progression");
            _compilationLog = Path.Combine(_tempDir, "compilation.log");
        }

        protected override void CheckParameters() {
            base.CheckParameters();
            if (FilesToCompile == null || FilesToCompile.Count == 0) {
                throw new ExecutionParametersException("No files specified");
            }
        }

        protected override void SetExecutionInfo() {
            
            base.SetExecutionInfo();

            // for each file of the list
            var filesListcontent = new StringBuilder();
            var count = 0;
            
            foreach (var file in FilesToCompile) {
                if (!File.Exists(file.CompiledPath)) {
                    throw new ExecutionException($"Couldn\'t find the source file : {file.CompiledPath.PrettyQuote()}");
                }

                var localSubTempDir = Path.Combine(_tempDir, count.ToString());
                var baseFileName = Path.GetFileNameWithoutExtension(file.CompiledPath);

                var compiledFile = new CompiledFile(file);
                CompiledFiles.Add(compiledFile);
                
                // get the output directory that will be use to generate the .r (and listing debug-list...)
                if (Path.GetExtension(file.SourcePath ?? "").Equals(OeConstants.ExtCls)) {
                    // for *.cls files, as many *.r files are generated, we need to compile in a temp directory
                    // we need to know which *.r files were generated for each input file
                    // so each file gets his own sub tempDir
                    compiledFile.CompilationOutputDir = Path.Combine(localSubTempDir, count.ToString());
                } else if (!string.IsNullOrEmpty(file.PreferedTargetPath)) {
                    compiledFile.CompilationOutputDir = file.PreferedTargetPath;
                } else {
                    compiledFile.CompilationOutputDir = localSubTempDir;
                }

                Utils.CreateDirectoryIfNeeded(compiledFile.CompilationOutputDir);

                compiledFile.CompOutputR = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{OeConstants.ExtR}");
                if (CompileWithListing) {
                    compiledFile.CompOutputLis = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{OeConstants.ExtLis}");
                }
                if (CompileWithXref) {
                    compiledFile.CompOutputXrf = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{(CompileUseXmlXref ? OeConstants.ExtXrfXml : OeConstants.ExtXrf)}");
                }
                if (CompileWithDebugList) {
                    compiledFile.CompOutputDbg = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{OeConstants.ExtDbg}");
                }
                if (IsAnalysisMode) {
                    if (!Directory.Exists(localSubTempDir)) {
                        Directory.CreateDirectory(localSubTempDir);
                    }
                    compiledFile.CompOutputFileIdLog = Path.Combine(localSubTempDir, $"{baseFileName}{OeConstants.ExtFileIdLog}");
                    compiledFile.CompOutputRefTables = Path.Combine(localSubTempDir, $"{baseFileName}{OeConstants.ExtRefTables}");
                }

                // feed files list
                filesListcontent.AppendLine($"{file.CompiledPath.ProQuoter()} {compiledFile.CompilationOutputDir.ProQuoter()} {compiledFile.CompOutputLis.ProQuoter()} {compiledFile.CompOutputXrf.ProQuoter()} {compiledFile.CompOutputDbg.ProQuoter()} {compiledFile.CompOutputFileIdLog.ProQuoter()} {compiledFile.CompOutputRefTables.ProQuoter()}");

                count++;
            }
            
            File.WriteAllText(_filesListPath, filesListcontent.ToString(), Encoding.Default);

            SetPreprocessedVar("CompileListFilePath", _filesListPath.PreProcQuoter());
            SetPreprocessedVar("CompileProgressionFilePath", _progressionFilePath.PreProcQuoter());
            SetPreprocessedVar("CompileLogPath", _compilationLog.PreProcQuoter());
            SetPreprocessedVar("IsAnalysisMode", IsAnalysisMode.ToString());
        }

        /// <summary>
        ///     Also publish the end of compilation events
        /// </summary>
        protected override void PublishExecutionEndEvents() {
            // end of successful execution action
            if (!ExecutionFailed) {
                // Analysis mode, read output files
                if (IsAnalysisMode) {
                    try {
                        Parallel.ForEach(CompiledFiles, file => {
                            file.ReadAnalysisResults();
                        });
                    } catch (Exception e) {
                        HandledExceptions.Add(new ExecutionException("Error while reading the analysis results", e));
                    }
                }
                
                try {
                    // read the global log error and assign compilation problems to concerrned compiled files
                    LoadErrorLog();
                    CompiledFiles.ForEach(f => f.CorrectRcodePathForClassFiles());

                    OnCompilationOk?.Invoke(this, CompiledFiles);
                } catch (Exception e) {
                    HandledExceptions.Add(new ExecutionException("Error while reading the compilation results", e));
                }
            }

            base.PublishExecutionEndEvents();
        }
        
        /// <summary>
        ///     Read the compilation/prolint errors of a given execution through its .log file
        ///     update the CompiledFiles accordingly
        /// </summary>
        private void LoadErrorLog() {
            Utils.ForEachLine(_compilationLog, new byte[0], (i, line) => {
                var fields = line.Split('\t').ToList();
                if (fields.Count == 7) {
                    var compiledFile = CompiledFiles.First(f => f.CompiledPath.Equals(fields[0], StringComparison.CurrentCultureIgnoreCase));

                    var error = new CompilationError {
                        SourcePath = fields[1],
                        Line = Math.Max(0, (int) fields[3].ConvertFromStr(typeof(int)) - 1),
                        Column = Math.Max(0, (int) fields[4].ConvertFromStr(typeof(int)) - 1),
                        ErrorNumber = Math.Max(0, (int) fields[5].ConvertFromStr(typeof(int)) - 1)
                    };
                    
                    var identicalError = compiledFile.CompilationErrors.FirstOrDefault(ce => ce.Line.Equals(error.Line) && ce.ErrorNumber.Equals(error.ErrorNumber));
                    if (identicalError != null) {
                        identicalError.Times = identicalError.Times == 0 ? 2 : identicalError.Times++;
                        return;
                    }
 
                    if (!Enum.TryParse(fields[2], true, out CompilationErrorLevel compilationErrorLevel))
                        compilationErrorLevel = CompilationErrorLevel.Error;
                    error.Level = compilationErrorLevel;

                    error.Message = fields[6].Replace("<br>", "\n").Replace(fields[0], compiledFile.BaseFileName).Trim();
                }
            });
        }

    }
    
}