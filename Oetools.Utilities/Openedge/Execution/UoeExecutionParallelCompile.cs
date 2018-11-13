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
using Oetools.Utilities.Lib;
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

        public int NumberOfProcessesRunning => _processes.Count(p => p.Started && !p.Ended);

        private UoeExecution _firstProcessWithExceptions;
        
        private static object _lock = new object();

        private List<UoeExecutionCompile> _processes = new List<UoeExecutionCompile>();
        
        public UoeExecutionParallelCompile(AUoeExecutionEnv env) : base(env) {}
        
        public override void Dispose() {
            try {
                foreach (var proc in _processes) {
                    proc?.Dispose();
                }
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error when disposing of the compilation processes.", e));
            }
        }

        protected override void CheckParameters() {
            base.CheckParameters();
            if (MinimumNumberOfFilesPerProcess <= 0) {
                throw new UoeExecutionParametersException($"Invalid parameter for {nameof(MinimumNumberOfFilesPerProcess)} = {MinimumNumberOfFilesPerProcess}, should be at least 1.");
            }
            if (MaxNumberOfProcesses <= 0) {
                throw new UoeExecutionParametersException($"Invalid parameter for {nameof(MaxNumberOfProcesses)} = {MaxNumberOfProcesses}, should be at least 1.");
            }
        }

        protected override void StartInternal() {
            CheckParameters();
            
            // ensure that each process will at least take in 10 files, starting a new process for 1 file to compile isn't efficient!
            var numberOfProcesses = Math.Min(MaxNumberOfProcesses, NumberOfFilesToCompile / MinimumNumberOfFilesPerProcess);
            numberOfProcesses = Math.Max(numberOfProcesses, 1);

            var fileLists = new List<PathList<UoeFileToCompile>>();
            var currentProcess = 0;
            
            // foreach, sorted from the biggest (in size) to the smallest file
            foreach (var file in FilesToCompile.OrderByDescending(compile => compile.FileSize)) {
                
                // create a new process when needed
                if (currentProcess >= fileLists.Count) {
                    fileLists.Add(new PathList<UoeFileToCompile>());
                }

                // assign the file to the current process
                if (!fileLists[currentProcess].TryAdd(file)) {
                    continue;
                }

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
                    WorkingDirectory = WorkingDirectory,
                    StopOnCompilationError = StopOnCompilationError,
                    StopOnCompilationWarning = StopOnCompilationWarning
                };
                exec.OnExecutionException += OnProcessExecutionException;
                exec.OnExecutionEnd += OnProcessExecutionEnd;
                _processes.Add(exec);
            }

            // launch the compile process
            try {
                foreach (var proExecutionCompile in _processes) {
                    proExecutionCompile.Start();
                }
            } catch (Exception) {
                Started = true;
                KillProcess();
                throw;
            }
        }

        public override void KillProcess() {
            if (StartDateTime == null) {
                return;
            }
            if (!HasBeenKilled) {
                HasBeenKilled = true;
                // wait for the execution to start all the processes
                var d = DateTime.Now;
                while (!Started && DateTime.Now.Subtract(d).TotalMilliseconds <= 10000) { }
                try {
                    foreach (var proc in _processes) {
                        proc.KillProcess();
                    }
                } catch (Exception e) {
                    HandledExceptions.Add(new UoeExecutionException("Error when killing compilation processes.", e));
                } finally {
                    // wait for all the processes to actually exit correctly and publish their events
                    d = DateTime.Now;
                    while (NumberOfProcessesRunning > 0 && DateTime.Now.Subtract(d).TotalMilliseconds <= 10000) { }
                    PublishParallelCompilationResults();
                }
            }
        }

        /// <summary>
        /// Wait for the compilation to end
        /// Returns true if the process has exited (can be false if timeout was reached)
        /// </summary>
        /// <param name="maxWait"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public override bool WaitForExecutionEnd(int maxWait = 0, CancellationToken? cancelToken = null) {
            if (!Started) {
                return true;
            }

            bool hasMaxWait = maxWait > 0;
            bool exited = true;
            foreach (var proc in _processes) {
                var d = DateTime.Now;
                exited = exited && proc.WaitForExecutionEnd(maxWait, cancelToken);
                maxWait -= (int) DateTime.Now.Subtract(d).TotalMilliseconds;
                if (hasMaxWait && maxWait <= 0) {
                    return false;
                }
            }
            return exited;
        }
        
        private void OnProcessExecutionException(UoeExecution obj) {
            if (_firstProcessWithExceptions == null) {
                _firstProcessWithExceptions = obj;
            }
            // if one fails, all must fail
            KillProcess();
        }

        private void OnProcessExecutionEnd(UoeExecution obj) {
            // add compiled files to this list
            Monitor.Enter(_lock);
            try {
                if (obj is UoeExecutionCompile compilation) {
                    CompiledFiles.TryAddRange(compilation.CompiledFiles);
                }        
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error when checking the compilation results.", e));
            } finally {
                Monitor.Exit(_lock);
            }
            
            // only do the rest when reaching the last process
            if (NumberOfProcessesRunning > 0) {
                return;
            }

            if (!HasBeenKilled) {
                PublishParallelCompilationResults();
            }
        }

        private void PublishParallelCompilationResults() {
            Monitor.Enter(_lock);
            try {
                Ended = true;
                ExecutionTimeSpan = TimeSpan.FromMilliseconds(DateTime.Now.Subtract(StartDateTime ?? DateTime.Now).TotalMilliseconds);
                HandledExceptions.Clear();
                HandledExceptions.AddRange(_processes.SelectMany(p => p.HandledExceptions).Where(e => !(e is UoeExecutionKilledException)));
                DatabaseConnectionFailed = _processes.Exists(p => p.DatabaseConnectionFailed);
                ExecutionFailed = _processes.Exists(p => p.ExecutionFailed);
                
                // we clear all the killed exception because if one process fails, we kill the others
                // The only killed exception that matter is on the first process that failed with exceptions
                if (_firstProcessWithExceptions != null && _firstProcessWithExceptions.ExecutionHandledExceptions && _firstProcessWithExceptions.HandledExceptions.Any(e => e is UoeExecutionKilledException)) {
                    HandledExceptions.Add(new UoeExecutionKilledException());
                }
            } finally {
                Monitor.Exit(_lock);
                PublishEndEvents();
            }
        }
    }
}