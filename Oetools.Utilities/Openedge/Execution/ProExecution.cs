// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProExecution.cs) is part of csdeployer.
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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    ///     Base class for all the progress execution (i.e. when we need to start a prowin process and do something)
    /// </summary>
    public abstract class ProExecution : IDisposable {
        
        /// <summary>
        ///     The action to execute just after the end of a prowin process
        /// </summary>
        public event Action<ProExecution> OnExecutionEnd;

        /// <summary>
        ///     The action to execute at the end of the process if it went well
        /// </summary>
        public event Action<ProExecution> OnExecutionOk;

        /// <summary>
        ///     The action to execute at the end of the process if something went wrong
        /// </summary>
        public event Action<ProExecution> OnExecutionFailed;

        /// <summary>
        ///     set to true if a valid database connection is mandatory (if so, failing to connect will be considered as an error)
        /// </summary>
        public bool NeedDatabaseConnection { get; set; }

        /// <summary>
        ///     Environment to use
        /// </summary>
        public IEnvExecution Env { get; }

        /// <summary>
        ///     set to true if a the execution process has been killed
        /// </summary>
        public bool HasBeenKilled { get; private set; }

        /// <summary>
        ///     Set to true after the process is over if the execution failed
        /// </summary>
        public bool ExecutionFailed { get; private set; }

        /// <summary>
        ///     Set to true after the process is over if the database connection has failed
        /// </summary>
        public bool DbConnectionFailed { get; private set; }

        /// <summary>
        /// List of handled exceptions
        /// </summary>
        public List<ExecutionException> HandledExceptions { get; } = new List<ExecutionException>();
        
        /// <summary>
        /// if the exe should be executed silently (hidden) or not
        /// </summary>
        protected virtual bool SilentExecution => false;
        
        /// <summary>
        /// can only be executed with the gui version of progres (e.g. windows only)
        /// </summary>
        protected virtual bool RequiresGraphicalMode => false;
        
        protected readonly Dictionary<string, string> PreprocessedVars;

        /// <summary>
        ///     Path to the output .log file (for compilation)
        /// </summary>
        protected string _ErrorLogPath;

        /// <summary>
        ///     log to the database connection log (not existing if everything is ok)
        /// </summary>
        protected string _dbErrorLogPath;

        /// <summary>
        ///     Full path to the directory containing all the files needed for the execution
        /// </summary>
        protected string _localTempDir;

        /// <summary>
        ///     Full path to the directory used as the working directory to start the prowin process
        /// </summary>
        protected string _processStartDir;

        protected string _propathFilePath;

        /// <summary>
        ///     Parameters of the .exe call
        /// </summary>
        protected StringBuilder _exeParameters;

        protected ProgressProcessIo _process;

        protected string _runnerPath;

        /// <summary>
        ///     Deletes temp directory and everything in it
        /// </summary>
        public void Dispose() {
            try {
                _process?.Dispose();

                // delete temp dir
                if (_localTempDir != null) {
                    Utils.DeleteDirectoryIfExists(_localTempDir, true);
                }
            } catch (Exception e) {
                HandledExceptions.Add(new ExecutionException("Error when disposing of the process", e));
            }
        }

        public ProExecution(IEnvExecution env) {
            Env = env;
            PreprocessedVars = new Dictionary<string, string>();
            
            // unique temporary folder
            _localTempDir = Path.Combine(Env.TempDirectory, $"exec_{DateTime.Now:HHmmssfff}_{Path.GetRandomFileName()}");
            _ErrorLogPath = Path.Combine(_localTempDir, "run.errors");
            _dbErrorLogPath = Path.Combine(_localTempDir, "db.errors");
            _propathFilePath = Path.Combine(_localTempDir, "oe.propath");
            _runnerPath = Path.Combine(_localTempDir, $"run_{DateTime.Now:HHmmssfff}.p");
            _processStartDir = _localTempDir;
        }
        
        /// <summary>
        ///     allows to prepare the execution environment by creating a unique temp folder
        ///     and copying every critical files into it
        ///     Then execute the progress program
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ExecutionException"></exception>
        public void Start() {
            
            // check parameters
            CheckParameters();
            
            Utils.CreateDirectoryIfNeeded(_localTempDir);

            // write propath
            File.WriteAllText(_propathFilePath, $"{_localTempDir + "," + string.Join(",", Env.ProPathList)}", Encoding.Default);

            // Set info
            SetExecutionInfo();
            SetPreprocessedVar("ErrorLogPath", _ErrorLogPath.PreProcQuoter());
            SetPreprocessedVar("DbErrorLogPath", _dbErrorLogPath.PreProcQuoter());
            SetPreprocessedVar("PropathFilePath", _propathFilePath.PreProcQuoter());
            SetPreprocessedVar("DbConnectString", Env.DatabaseConnectionString.PreProcQuoter());
            SetPreprocessedVar("DatabaseAliasList", (Env.DatabaseAliases != null ? string.Join(";", Env.DatabaseAliases.Select(a => $"{a.AliasLogicalName},{a.DatabaseLogicalName}")) : "").PreProcQuoter()); // Format : ALIAS,DATABASE;ALIAS2,DATABASE;...
            SetPreprocessedVar("DbConnectionRequired", NeedDatabaseConnection.ToString());
            SetPreprocessedVar("PreExecutionProgram", Env.PreExecutionProgramPath.Trim().PreProcQuoter());
            SetPreprocessedVar("PostExecutionProgram", Env.PostExecutionProgramPath.Trim().PreProcQuoter());

            // prepare the .p runner
            var runnerProgram = new StringBuilder();
            foreach (var var in PreprocessedVars) {
                runnerProgram.AppendLine($"&SCOPED-DEFINE {var.Key} {var.Value}");
            }
            runnerProgram.Append(ProgramProgressRun);
            SetProgramToRun(runnerProgram);
            File.WriteAllText(_runnerPath, runnerProgram.ToString(), Encoding.Default);

            // Parameters
            _exeParameters = new StringBuilder($"-p {_runnerPath.Quoter()}");
            AppendProgressParameters(_exeParameters);
            if (!string.IsNullOrWhiteSpace(Env.ProExeCommandLineParameters)) {
                _exeParameters.Append($" {Env.ProExeCommandLineParameters.Trim()}");
            }
            if (!string.IsNullOrEmpty(Env.IniFilePath)) {
                _exeParameters.Append($" -ininame {Env.IniFilePath.Quoter()} -basekey {"INI".Quoter()}");
            }

            // start the process
            _process = new ProgressProcessIo(Env.DlcDirectoryPath, Env.UseProgressCharacterMode && !RequiresGraphicalMode, Env.CanProVersionUseNoSplash) {
                WorkingDirectory = _processStartDir
            };
            _process.OnProcessExit += ProcessOnExited;
            _process.ExecuteAsync(_exeParameters.ToString(), SilentExecution);
        }

        /// <summary>
        ///     Allows to kill the process of this execution (be careful, the OnExecutionEnd, Ok, Fail events are not executed in
        ///     that case!)
        /// </summary>
        public void KillProcess() {
            try {
                _process.Kill();
            } catch (Exception e) {
                HandledExceptions.Add(new ExecutionException("Error when killing the process", e));
            }
            HasBeenKilled = true;
        }

        public void WaitForProcessExit(int maxWait = 0) {
            if (maxWait > 0) {
                _process?.WaitForExit(maxWait);
            } else {
                _process?.WaitForExit();
            }
        }

        /// <summary>
        ///     Should return null or the message error that indicates which parameter is incorrect
        /// </summary>
        /// <exception cref="ExecutionException"></exception>
        protected virtual void CheckParameters() {
            // check prowin
            if (!Directory.Exists(Env.DlcDirectoryPath)) {
                throw new ExecutionParametersException($"Couldn\'t start an execution, the DLC directory does not exist : {Env.DlcDirectoryPath.Quoter()}");
            }
        }
        
        protected virtual void SetProgramToRun(StringBuilder runnerProgram) {}
        
        /// <summary>
        ///     Extra stuff to do before executing
        /// </summary>
        /// <exception cref="ExecutionException"></exception>
        protected virtual void SetExecutionInfo() { }

        /// <summary>
        ///     Add stuff to the command line
        /// </summary>
        protected virtual void AppendProgressParameters(StringBuilder sb) {
        }

        /// <summary>
        ///     set pre-processed variable for the runner program
        /// </summary>
        protected void SetPreprocessedVar(string key, string value) {
            if (!PreprocessedVars.ContainsKey(key))
                PreprocessedVars.Add(key, value);
            else
                PreprocessedVars[key] = value;
        }

        /// <summary>
        ///     Called by the process's thread when it is over, execute the ProcessOnExited event
        /// </summary>
        private void ProcessOnExited(object sender, EventArgs eventArgs) {
            try {
                // if the db log file exists, then the connect statement failed
                if (File.Exists(_dbErrorLogPath)) {
                    HandledExceptions.AddRange(GetOpenedgeExceptions<ExecutionOpenedgeDbConnectionException>(_dbErrorLogPath));
                    DbConnectionFailed = true;
                }
                
                if (_process.ExitCode > 0) {
                    HandledExceptions.Add(new ExecutionProcessException(_process.ExecutablePath, _process.StartParameters, _process.WorkingDirectory, _process.BatchModeOutput.ToString(), _process.ExitCode));
                    ExecutionFailed = true;              

                } else if (!File.Exists(_ErrorLogPath)) {
                    // the log file wasn't created, indicating that the procedure didn't run until the end correctly
                    HandledExceptions.Add(new ExecutionProcessException(_process.ExecutablePath, _process.StartParameters, _process.WorkingDirectory, _process.BatchModeOutput.ToString(), _process.ExitCode));
                    ExecutionFailed = true;     
                    
                } else if (new FileInfo(_ErrorLogPath).Length > 0) {
                    // else if the log isn't empty, something went wrong
                    HandledExceptions.AddRange(GetOpenedgeExceptions<ExecutionOpenedgeException>(_ErrorLogPath));
                    ExecutionFailed = true;
                }

            } catch (Exception e) {
                HandledExceptions.Add(new ExecutionException("Error when checking the process results", e));
                ExecutionFailed = true;
            } finally {
                PublishExecutionEndEvents();
            }
        }

        /// <summary>
        /// Read the exceptions from a log file
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private List<ExecutionOpenedgeException> GetOpenedgeExceptions<T>(string filePath) where T : ExecutionOpenedgeException, new() {
            var output = new List<ExecutionOpenedgeException>();
            if (File.Exists(filePath)) {
                Utils.ForEachLine(filePath, null, (i, line) => {
                    var split = line.Split('\t');
                    if (split.Length == 2) {
                        var t = new T {
                            ErrorNumber = int.Parse(split[0]),
                            ErrorMessage = split[1]
                        };
                        output.Add(t);
                    }
                }, Encoding.Default);
            }
            return output;
        }

        /// <summary>
        ///     publish the end of execution events
        /// </summary>
        protected virtual void PublishExecutionEndEvents() {
            // end of successful/unsuccessful execution action
            try {
                if (ExecutionFailed || DbConnectionFailed && NeedDatabaseConnection) {
                    OnExecutionFailed?.Invoke(this);
                } else {
                    OnExecutionOk?.Invoke(this);
                }
            } catch (Exception e) {
                HandledExceptions.Add(new ExecutionException("Error in published events", e));
            }

            // end of execution action
            try {
                OnExecutionEnd?.Invoke(this);
            } catch (Exception e) {
                HandledExceptions.Add(new ExecutionException("Error in published events 2", e));
            }
        }
        
        private string ProgramProgressRun => OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution.p");

    }
}