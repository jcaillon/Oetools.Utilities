#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionParallelCompile.cs) is part of Oetools.Utilities.
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
using System.Linq;
using System.Threading;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Openedge.Execution {
    
    public class UoeExecutionParallelCompile : UoeExecutionCompile {

        public int MaxNumberOfProcesses { get; set; } = 1;

        public virtual int MinimumNumberOfFilesPerProcess { get; set; } = 10;

        public override int NumberOfFilesTreated {
            get {
                var nbFilesDone = 0;
                foreach (var proc in _processes.Where(p => p != null)) {
                    nbFilesDone += proc.NumberOfFilesTreated;
                }
                return nbFilesDone;
            }
        }

        public int TotalNumberOfProcesses => _processes.Count;

        public int NumberOfProcessesRunning => _processes.Count(p => p.ExecutionTimeSpan == null);
        
        private static object _lock = new object();
        
        private List<UoeExecutionCompile> _processes = new List<UoeExecutionCompile>();
        
        public UoeExecutionParallelCompile(IUoeExecutionEnv env) : base(env) {}
        
        public override void Dispose() {
            try {
                foreach (var proc in _processes) {
                    proc?.Dispose();
                }
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error when disposing of the compilation processes", e));
            }
        }

        protected override void CheckParameters() {
            base.CheckParameters();
            if (MinimumNumberOfFilesPerProcess <= 0) {
                throw new UoeExecutionParametersException($"Invalid parameter for {nameof(MinimumNumberOfFilesPerProcess)} = {MinimumNumberOfFilesPerProcess}, should be at least 1");
            }
            if (MaxNumberOfProcesses <= 0) {
                throw new UoeExecutionParametersException($"Invalid parameter for {nameof(MaxNumberOfProcesses)} = {MaxNumberOfProcesses}, should be at least 1");
            }
        }

        public override void Start() {
            StartDateTime = DateTime.Now;
            CheckParameters();
            
            // now we do a list of those files, sorted from the biggest (in size) to the smallest file
            FilesToCompile.Sort((file1, file2) => file2.FileSize.CompareTo(file1.FileSize));
            
            // ensure that each process will at least take in 10 files, starting a new process for 1 file to compile isn't efficient!
            var numberOfProcesses = Math.Min(MaxNumberOfProcesses, NumberOfFilesToCompile / MinimumNumberOfFilesPerProcess);
            numberOfProcesses = Math.Max(numberOfProcesses, 1);

            var fileLists = new List<List<UoeFileToCompile>>();
            var currentProcess = 0;
            foreach (var file in FilesToCompile) {
                // create a new process when needed
                if (currentProcess >= fileLists.Count) {
                    fileLists.Add(new List<UoeFileToCompile>());
                }

                // assign the file to the current process
                fileLists[currentProcess].Add(file);

                // we will assign the next file to the next process...
                currentProcess++;
                if (currentProcess == numberOfProcesses) {
                    currentProcess = 0;
                }
            }

            // init the compilation on each process
            for (var i = 0; i < numberOfProcesses; i++) {
                var exec = new UoeExecutionCompile(Env) {
                    FilesToCompile = fileLists[i],
                    AnalysisModeSimplifiedDatabaseReferences = AnalysisModeSimplifiedDatabaseReferences,
                    CompileInAnalysisMode = CompileInAnalysisMode,
                    CompileOptions = CompileOptions,
                    CompilerMultiCompile = CompilerMultiCompile,
                    CompileStatementExtraOptions = CompileStatementExtraOptions,
                    CompileWithDebugList = CompileWithDebugList,
                    CompileWithListing = CompileWithListing,
                    CompileWithPreprocess = CompileWithPreprocess,
                    CompileWithXref = CompileWithXref,
                    CompileUseXmlXref = CompileUseXmlXref,
                    WorkingDirectory = WorkingDirectory
                };
                exec.OnExecutionException += OnProcessExecutionException;
                exec.OnExecutionEnd += OnProcessExecutionEnd;
                _processes.Add(exec);
            }

            // launch the compile process
            foreach (var proExecutionCompile in _processes) {
                proExecutionCompile.Start();
            }
        }

        public override void KillProcess() {
            if (!HasBeenKilled) {
                try {
                    foreach (var proc in _processes) {
                        proc.KillProcess();
                    }
                } catch (Exception e) {
                    HandledExceptions.Add(new UoeExecutionException("Error when killing compilation processes", e));
                }
            }
            HasBeenKilled = true;
        }

        public override void WaitForExecutionEnd(int maxWait = 0) {
            foreach (var proc in _processes) {
                proc.WaitForExecutionEnd(maxWait);
            }
        }
        
        private void OnProcessExecutionException(UoeExecution obj) {
            // if one fails, all must fail
            KillProcess();
        }

        private void OnProcessExecutionEnd(UoeExecution obj) {
            // add compiled files to this list
            Monitor.Enter(_lock);
            try {
                if (obj is UoeExecutionCompile compilation) {
                    CompiledFiles.AddRange(compilation.CompiledFiles);
                }        
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error when checking the compilation results", e));
            } finally {
                Monitor.Exit(_lock);
            }
            
            // only do the rest when reaching the last process
            if (NumberOfProcessesRunning > 0) {
                return;
            }
            
            try {
                ExecutionTimeSpan = TimeSpan.FromMilliseconds(DateTime.Now.Subtract(StartDateTime).TotalMilliseconds);
                HandledExceptions.AddRange(_processes.SelectMany(p => p.HandledExceptions));
                DatabaseConnectionFailed = _processes.Exists(p => p.DatabaseConnectionFailed);
                ExecutionFailed = _processes.Exists(p => p.ExecutionFailed);
            } finally {
                PublishEndEvents();
            }
        }
    }
}