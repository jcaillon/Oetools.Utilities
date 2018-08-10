﻿// ========================================================================
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
using System.Text;
using System.Threading.Tasks;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    public abstract class OeExecutionHandleCompilation : OeExecution {

        /// <summary>
        /// Number of files already treated
        /// </summary>
        public int NumberOfFilesTreated => unchecked((int) (File.Exists(_progressionFilePath) ? new FileInfo(_progressionFilePath).Length : 0));
       
        /// <summary>
        ///     When true, we activate the log just before compiling with FileId active + we use xref to generate a file that list the referenced
        ///     table of the .r
        /// </summary>
        public bool CompileInAnalysisMode { get; set; }
       
        /// <summary>
        /// A "cheapest" analysis mode where we don't compute the database references from the xref file but use the
        /// RCODE-INFO:TABLE-LIST instead
        /// Less accurate since it won't list referenced sequences or referenced tables in LIKE TABLE statements
        /// </summary>
        public bool AnalysisModeSimplifiedDatabaseReferences { get; set; }

        /// <summary>
        /// Activate the debug list compile option, preprocess the file replacing preproc variables and includes and printing the result
        /// each line is numbered
        /// </summary>
        public bool CompileWithDebugList { get; set; }
        
        /// <summary>
        /// Activate the xref compile option
        /// </summary>
        public bool CompileWithXref { get; set; }
        
        /// <summary>
        /// Activate the listing compile option
        /// </summary>
        public bool CompileWithListing { get; set; }
        
        /// <summary>
        /// Activate the xml-xref compile option,
        /// it is incompatible with <see cref="CompileInAnalysisMode"/> but you can analyze and generate xml-xref at the same time
        /// using <see cref="AnalysisModeSimplifiedDatabaseReferences"/>
        /// </summary>
        public bool CompileUseXmlXref { get; set; }
        
        /// <summary>
        /// Activate the preprocess compile option which is a listing file exactly like <see cref="CompileWithDebugList"/> except it
        /// doesn't print the line numbers
        /// </summary>
        public bool CompileWithPreprocess { get; set; }
        
        /// <summary>
        /// Sets the COMPILER:MULTI-COMPILE value
        /// </summary>
        public bool CompilerMultiCompile { get; set; }
        
        /// <summary>
        /// Extra options to add to the compile statement, for instance "MIN-SIZE = TRUE"
        /// </summary>
        public string CompileStatementExtraOptions { get; set; }
        
        /// <summary>
        /// OPTIONS option of COMPILE only since 11.7 : require-full-names,require-field-qualifiers,require-full-keywords
        /// </summary>
        public string CompileOptions { get; set; }

        /// <summary>
        /// List of the files to compile / run / prolint
        /// </summary>
        public List<FileToCompile> FilesToCompile { get; set; }

        /// <summary>
        /// List of the compiled files
        /// </summary>
        public List<CompiledFile> CompiledFiles { get; } = new List<CompiledFile>();

        /// <summary>
        /// Path to the file containing the COMPILE output
        /// </summary>
        private string _compilationLog;
        
        private string _filesListPath;
        
        private bool _useXmlXref;
        
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
            if ((CompileInAnalysisMode || AnalysisModeSimplifiedDatabaseReferences) && !Env.IsProVersionHigherOrEqualTo(new Version(10, 2, 0))) {
                throw new ExecutionParametersException("The analysis mode (computes file and database references required to compile) is only available for openedge >= 10.2");
            }
        }

        protected override void SetExecutionInfo() {

            _useXmlXref = CompileUseXmlXref && (!CompileInAnalysisMode || AnalysisModeSimplifiedDatabaseReferences);
            
            base.SetExecutionInfo();

            // for each file of the list
            var filesListcontent = new StringBuilder();
            var count = 0;
            
            foreach (var file in FilesToCompile) {
                if (!File.Exists(file.CompiledPath)) {
                    throw new ExecutionParametersException($"Couldn\'t find the source file : {file.CompiledPath.PrettyQuote()}");
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
                    compiledFile.CompilationOutputDirectory = localSubTempDir;
                } else if (!string.IsNullOrEmpty(file.PreferedTargetDirectory)) {
                    compiledFile.CompilationOutputDirectory = file.PreferedTargetDirectory;
                } else {
                    compiledFile.CompilationOutputDirectory = localSubTempDir;
                }

                Utils.CreateDirectoryIfNeeded(compiledFile.CompilationOutputDirectory);

                compiledFile.CompilationRcodeFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtR}");
                compiledFile.CompilationErrorsFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtCompileErrorsLog}");
                if (CompileWithListing) {
                    compiledFile.CompilationListingFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtListing}");
                }
                
                if (CompileWithXref && !_useXmlXref) {
                    compiledFile.CompilationXrefFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtXref}");
                }
                if (CompileWithXref && _useXmlXref) {
                    compiledFile.CompilationXmlXrefFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtXrefXml}");
                }
                if (CompileWithDebugList) {
                    compiledFile.CompilationDebugListFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtDebugList}");
                }
                if (CompileWithPreprocess) {
                    compiledFile.CompilationPreprocessedFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtPreprocessed}");
                }

                compiledFile.IsAnalysisMode = CompileInAnalysisMode;
                
                if (CompileInAnalysisMode) {
                    Utils.CreateDirectoryIfNeeded(localSubTempDir);
                    compiledFile.CompilationFileIdLogFilePath = Path.Combine(localSubTempDir, $"{baseFileName}{OeConstants.ExtFileIdLog}");
                    if (AnalysisModeSimplifiedDatabaseReferences) {
                        compiledFile.CompilationRcodeTableListFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{OeConstants.ExtTableList}");
                    } else if (string.IsNullOrEmpty(compiledFile.CompilationXrefFilePath)) {
                        compiledFile.CompilationXrefFilePath = Path.Combine(localSubTempDir, $"{baseFileName}{OeConstants.ExtXref}");
                    }
                }

                // feed files list
                filesListcontent
                    .Append(file.CompiledPath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationOutputDirectory.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationErrorsFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationListingFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationXrefFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationXmlXrefFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationDebugListFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationPreprocessedFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationFileIdLogFilePath.ProExportFormat())
                    .Append(" ")
                    .Append(compiledFile.CompilationRcodeTableListFilePath.ProExportFormat())
                    .AppendLine();

                count++;
            }
            
           
            File.WriteAllText(_filesListPath, filesListcontent.ToString(), Encoding.Default);

            SetPreprocessedVar("CompileListFilePath", _filesListPath.ProPreProcStringify());
            SetPreprocessedVar("CompileProgressionFilePath", _progressionFilePath.ProPreProcStringify());
            SetPreprocessedVar("CompileLogPath", _compilationLog.ProPreProcStringify());
            SetPreprocessedVar("IsAnalysisMode", CompileInAnalysisMode.ToString());
            SetPreprocessedVar("GetRcodeTableList", AnalysisModeSimplifiedDatabaseReferences.ToString());
            SetPreprocessedVar("ProVerHigherOrEqualTo10.2", Env.IsProVersionHigherOrEqualTo(new Version(10, 2, 0)).ToString());
            SetPreprocessedVar("UseXmlXref", _useXmlXref.ToString());
            SetPreprocessedVar("CompileStatementExtraOptions", CompileStatementExtraOptions.ProPreProcStringify().StripQuotes());
            SetPreprocessedVar("CompilerMultiCompile", CompilerMultiCompile.ToString());
            SetPreprocessedVar("ProVerHigherOrEqualTo11.7", Env.IsProVersionHigherOrEqualTo(new Version(11, 7, 0)).ToString());
            SetPreprocessedVar("CompileOptions", CompileOptions.ProPreProcStringify());
        }

        protected override void AppendProgramToRun(StringBuilder runnerProgram) {
            base.AppendProgramToRun(runnerProgram);
            runnerProgram.AppendLine(ProgramProgressCompile);
        }
        
        private string ProgramProgressCompile => OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution_compile.p");

        /// <summary>
        ///     Also publish the end of compilation events
        /// </summary>
        protected override void PublishExecutionEndEvents() {
            // end of successful execution action
            if (!ExecutionFailed) {
                try {
                    Parallel.ForEach(CompiledFiles, file => {
                        file.ReadCompilationResults();
                    });
                } catch (Exception e) {
                    HandledExceptions.Add(new ExecutionException("Error while reading the compilation results", e));
                }
            }

            base.PublishExecutionEndEvents();
        }
        
    }
    
}