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
using Oetools.Utilities.Openedge;

namespace Oetools.Builder.Core2.Execution {

    public abstract class ProExecutionHandleCompilation : ProExecution {

        protected override ExecutionType ExecutionType => ExecutionType.Compile;
        
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
        public event Action<ProExecutionHandleCompilation, List<CompiledFile>> OnCompilationOk;

        /// <summary>
        /// List of the files to compile / run / prolint
        /// </summary>
        public List<FileToCompile> FilesToCompile { get; set; }

        protected List<CompiledFile> CompiledFiles { get; } = new List<CompiledFile>();

        /// <summary>
        /// Pro environment to use
        /// </summary>
        public new IEnvExecutionCompilation Env { get; }

        /// <summary>
        /// Path to the file containing the COMPILE output
        /// </summary>
        protected string _compilationLog;

        /// <summary>
        /// Path to a file used to determine the progression of a compilation (useful when compiling multiple programs)
        /// 1 byte = 1 file treated
        /// </summary>
        private string _progressionFilePath;

        public ProExecutionHandleCompilation(IEnvExecutionCompilation env) : base(env) {
            Env = env;
        }

        protected override void CheckParameters() {
            base.CheckParameters();
            if (FilesToCompile == null) {
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
                    throw new ExecutionException($"Couldn\'t find the source file : {file.CompiledPath.Quoter()}");
                }

                var localSubTempDir = Path.Combine(_localTempDir, count.ToString());
                var baseFileName = Path.GetFileNameWithoutExtension(file.CompiledPath);

                // get the output directory that will be use to generate the .r (and listing debug-list...)
                var compiledFile = new CompiledFile(file);
                CompiledFiles.Add(compiledFile);
                
                if (Path.GetExtension(file.SourcePath ?? "").Equals(OeConstants.ExtCls)) {
                    // for *.cls files, as many *.r files are generated, we need to compile in a temp directory
                    // we need to know which *.r files were generated for each input file
                    // so each file gets his own sub tempDir
                    compiledFile.CompilationOutputDir = Path.Combine(localSubTempDir, count.ToString());
                } else if (Env.CompileForceUseOfTemp || string.IsNullOrEmpty(file.PreferedTargetPath)) {
                    compiledFile.CompilationOutputDir = localSubTempDir;
                } else {
                    compiledFile.CompilationOutputDir = file.PreferedTargetPath;
                }

                Utils.CreateDirectoryIfNeeded(compiledFile.CompilationOutputDir);

                compiledFile.CompOutputR = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{OeConstants.ExtR}");
                if (Env.CompileWithListing)
                    compiledFile.CompOutputLis = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{OeConstants.ExtLis}");
                if (Env.CompileWithXref)
                    compiledFile.CompOutputXrf = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{(Env.CompileUseXmlXref ? OeConstants.ExtXrfXml : OeConstants.ExtXrf)}");
                if (Env.CompileWithDebugList)
                    compiledFile.CompOutputDbg = Path.Combine(compiledFile.CompilationOutputDir, $"{baseFileName}{OeConstants.ExtDbg}");

                if (Env.IsAnalysisMode) {
                    if (!Directory.Exists(localSubTempDir)) {
                        Directory.CreateDirectory(localSubTempDir);
                    }
                    compiledFile.CompOutputFileIdLog = Path.Combine(localSubTempDir, $"{baseFileName}{OeConstants.ExtFileIdLog}");
                    compiledFile.CompOutputRefTables = Path.Combine(localSubTempDir, $"{baseFileName}{OeConstants.ExtRefTables}");
                }

                // feed files list
                filesListcontent.AppendLine($"{file.CompiledPath.Quoter()} {compiledFile.CompilationOutputDir.Quoter()} {(compiledFile.CompOutputLis ?? "?").Quoter()} {(compiledFile.CompOutputXrf ?? "?").Quoter()} {(compiledFile.CompOutputDbg ?? "?").Quoter()} {(compiledFile.CompOutputFileIdLog ?? "").Quoter()} {(compiledFile.CompOutputRefTables ?? "").Quoter()}");

                count++;
            }
            
            var filesListPath = Path.Combine(_localTempDir, "files.list");
            File.WriteAllText(filesListPath, filesListcontent.ToString(), Encoding.Default);

            _progressionFilePath = Path.Combine(_localTempDir, "compile.progression");
            _compilationLog = Path.Combine(_localTempDir, "compilation.log");

            SetPreprocessedVar("ToCompileListFile", filesListPath.PreProcQuoter());
            SetPreprocessedVar("CompileProgressionFile", _progressionFilePath.PreProcQuoter());
            SetPreprocessedVar("OutputPath", _compilationLog.PreProcQuoter());
            SetPreprocessedVar("AnalysisMode", Env.IsAnalysisMode.ToString());
        }

        /// <summary>
        ///     Also publish the end of compilation events
        /// </summary>
        protected override void PublishExecutionEndEvents() {
            // Analysis mode, read output files
            if (Env.IsAnalysisMode) {
                try {
                    // do a deployment action for each file
                    Parallel.ForEach(CompiledFiles, file => {
                        file.ReadAnalysisResults();
                    });
                } catch (Exception e) {
                    AddHandledExceptions(e, "An error occurred while reading the analysis results");
                }
            }

            // end of successful execution action
            if (!ExecutionFailed && (!ConnectionFailed || !NeedDatabaseConnection)) {
                try {
                    LoadErrorLog();
                    CompiledFiles.ForEach(f => f.CorrectRcodePathForClassFiles());

                    OnCompilationOk?.Invoke(this, CompiledFiles);
                } catch (Exception e) {
                    AddHandledExceptions(e, "An error occurred while reading the compilation results");
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
                if (fields.Count == 8) {
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
                    error.Help = fields[7].Replace("<br>", "\n").Trim();
                }
            });
        }

    }
    
}