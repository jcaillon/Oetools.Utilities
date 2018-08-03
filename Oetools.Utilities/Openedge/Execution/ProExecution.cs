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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge;
using Oetools.Utilities.Resources;

namespace Oetools.Packager.Core2.Execution {
    
    /// <summary>
    ///     Base class for all the progress execution (i.e. when we need to start a prowin process and do something)
    /// </summary>
    public abstract class ProExecution {
        
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

            // create a unique temporary folder
            _localTempDir = Path.Combine(Env.TempDirectory, $"exec_{DateTime.Now:HHmmssfff}_{Path.GetRandomFileName()}");
            if (!Directory.Exists(_localTempDir)) {
                Directory.CreateDirectory(_localTempDir);
            }

            // move .ini file into the execution directory
            if (File.Exists(Env.IniPath)) {
                _tempInifilePath = Path.Combine(_localTempDir, "base.ini");

                // we need to copy the .ini but we must delete the PROPATH= part, as stupid as it sounds, if we leave a huge PROPATH 
                // in this file, it increases the compilation time by a stupid amount... unbelievable i know, but trust me, it does...
                var encoding = TextEncodingDetect.GetFileEncoding(Env.IniPath);
                var fileContent = Utils.ReadAllText(Env.IniPath, encoding);
                var regex = new Regex("^PROPATH=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var matches = regex.Match(fileContent);
                if (matches.Success) {
                    fileContent = regex.Replace(fileContent, @"PROPATH=");
                }
                File.WriteAllText(_tempInifilePath, fileContent, encoding);
            }

            // set common info on the execution
            _processStartDir = _localTempDir;
            _logPath = Path.Combine(_localTempDir, "run.log");
            _dbLogPath = Path.Combine(_localTempDir, "db.ko");
            NotificationPath = Path.Combine(_localTempDir, "postExecution.notif");
            _propath = $"{(_localTempDir + "," + string.Join(",", Env.ProPathList)).Trim().Trim(',')}\r\n";
            _propathFilePath = Path.Combine(_localTempDir, "progress.propath");
            File.WriteAllText(_propathFilePath, _propath, Encoding.Default);

            // Set info
            SetExecutionInfo();
            SetPreprocessedVar("ExecutionType", ExecutionTypeName.ToUpper().PreProcQuoter());
            SetPreprocessedVar("LogPath", _logPath.PreProcQuoter());
            SetPreprocessedVar("PropathFilePath", _propathFilePath.PreProcQuoter());
            SetPreprocessedVar("DbConnectString", Env.ConnectionString.PreProcQuoter());
            SetPreprocessedVar("DbLogPath", _dbLogPath.PreProcQuoter());
            SetPreprocessedVar("DbConnectionMandatory", NeedDatabaseConnection.ToString());
            SetPreprocessedVar("NotificationOutputPath", NotificationPath.PreProcQuoter());
            SetPreprocessedVar("PreExecutionProgram", Env.PreExecutionProgramPath.Trim().PreProcQuoter());
            SetPreprocessedVar("PostExecutionProgram", Env.PostExecutionProgramPath.Trim().PreProcQuoter());
            if (Env.DatabaseAliases != null) {
                // Format : ALIAS,DATABASE;ALIAS2,DATABASE;...
                SetPreprocessedVar("DatabaseAliasList", string.Join(";", Env.DatabaseAliases.Select(a => $"{a.AliasLogicalName},{a.DatabaseLogicalName}")).PreProcQuoter());
            }

            // prepare the .p runner
            _runnerPath = Path.Combine(_localTempDir, $"run_{DateTime.Now:HHmmssfff}.p");
            var runnerProgram = new StringBuilder();
            foreach (var var in PreprocessedVars)
                runnerProgram.AppendLine($"&SCOPED-DEFINE {var.Key} {var.Value}");
            runnerProgram.Append(ProgramProgressRun);
            File.WriteAllText(_runnerPath, runnerProgram.ToString(), Encoding.Default);

            // Parameters
            _exeParameters = new StringBuilder();
            if (UseBatchMode) {
                _exeParameters.Append(" -b");
            } 
            
            if (!Env.IsProgressCharacterMode) {
                if (Env.CanProExeUseNoSplash) {
                    _exeParameters.Append(" -nosplash");
                } else {
                    DisableSplashScreen();
                }
            }

            _exeParameters.Append($" -p {_runnerPath.Quoter()}");
            if (!string.IsNullOrWhiteSpace(Env.ProExeCommandLineParameters)) {
                _exeParameters.Append($" {Env.ProExeCommandLineParameters.Trim()}");
            }
            AppendProgressParameters(_exeParameters);

            // start the process
            StartProcess();
        }

        /// <summary>
        ///     The action to execute just after the end of a prowin process
        /// </summary>
        public event Action<ProExecution> OnExecutionEnd;

        /// <summary>
        ///     The action to execute at the end of the process if it went well = we found a .log and the database is connected or
        ///     is not mandatory
        /// </summary>
        public event Action<ProExecution> OnExecutionOk;

        /// <summary>
        ///     The action to execute at the end of the process if something went wrong (no .log or database down)
        /// </summary>
        public event Action<ProExecution> OnExecutionFailed;

        /// <summary>
        ///     set to true if a valid database connection is mandatory (the compilation will not be done if a db can't be
        ///     connected
        /// </summary>
        public bool NeedDatabaseConnection { get; set; }

        /// <summary>
        ///     Copy of the pro env to use
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
        public bool ConnectionFailed { get; private set; }

        /// <summary>
        /// Execution type of the current class
        /// </summary>
        protected virtual ExecutionType ExecutionType => ExecutionType.Appbuilder;

        protected string ExecutionTypeName => ExecutionType.ToString();

        public List<ExecutionException> HandledExceptions { get; private set; }
        
        public bool DbConnectionFailedOnMaxUser => (Utils.ReadAllText(_dbLogPath, Encoding.Default) ?? "").Contains("(748)");
        
        /// <summary>
        ///     Full file path to the output file for the custom post-execution notification
        /// </summary>
        public string NotificationPath { get; private set; }

        protected string _tempInifilePath;

        protected readonly Dictionary<string, string> PreprocessedVars;

        /// <summary>
        ///     Path to the output .log file (for compilation)
        /// </summary>
        protected string _logPath;

        /// <summary>
        ///     log to the database connection log (not existing if everything is ok)
        /// </summary>
        protected string _dbLogPath;

        /// <summary>
        ///     Full path to the directory containing all the files needed for the execution
        /// </summary>
        protected string _localTempDir;

        /// <summary>
        ///     Full path to the directory used as the working directory to start the prowin process
        /// </summary>
        protected string _processStartDir;

        protected string _propath;

        protected string _propathFilePath;

        /// <summary>
        ///     Parameters of the .exe call
        /// </summary>
        protected StringBuilder _exeParameters;

        protected Process _process;

        protected string _runnerPath;

        private StringBuilder _batchModeOutput;

        /// <summary>
        ///     Deletes temp directory and everything in it
        /// </summary>
        ~ProExecution() {
            Clean();
        }

        public ProExecution(IEnvExecution env) {
            Env = env;
            PreprocessedVars = new Dictionary<string, string> {
                {
                    "LogPath", "\"\""
                }, {
                    "DbLogPath", "\"\""
                }, {
                    "PropathFilePath", "\"\""
                }, {
                    "DbConnectString", "\"\""
                }, {
                    "ExecutionType", "\"\""
                }, {
                    "CurrentFilePath", "\"\""
                }, {
                    "OutputPath", "\"\""
                }, {
                    "ToCompileListFile", "\"\""
                }, {
                    "AnalysisMode", "false"
                }, {
                    "CompileProgressionFile", "\"\""
                }, {
                    "DbConnectionMandatory", "false"
                }, {
                    "NotificationOutputPath", "\"\""
                }, {
                    "PreExecutionProgram", "\"\""
                }, {
                    "PostExecutionProgram", "\"\""
                }, {
                    "DatabaseExtractCandoTblType", "\"\""
                }, {
                    "DatabaseExtractCandoTblName", "\"\""
                }, {
                    "DatabaseAliasList", "\"\""
                },
            };
        }

        /// <summary>
        ///     Allows to kill the process of this execution (be careful, the OnExecutionEnd, Ok, Fail events are not executed in
        ///     that case!)
        /// </summary>
        public void KillProcess() {
            try {
                _process.Kill();
                _process.Close();
            } catch (Exception e) {
                AddHandledExceptions(e);
            }
            HasBeenKilled = true;
        }

        public void WaitForProcessExit(int maxWait = 3000) {
            if (maxWait > 0)
                _process.WaitForExit(maxWait);
            else
                _process.WaitForExit();
        }

        /// <summary>
        ///     Should return null or the message error that indicates which parameter is incorrect
        /// </summary>
        /// <exception cref="ExecutionException"></exception>
        protected virtual void CheckParameters() {
            // check prowin
            if (!File.Exists(Env.ProExePath)) {
                throw new ExecutionParametersException($"Couldn\'t start an execution, the following file does not exist : {Env.ProExePath.Quoter()}");
            }
        }

        /// <summary>
        ///     Return true if can use batch mode
        /// </summary>
        protected virtual bool CanUseBatchMode => false;
        
        /// <summary>
        /// if the exe should be executed silently (hidden) or not
        /// </summary>
        protected virtual bool SilentExecution => false;
        
        protected bool UseBatchMode => !Env.NeverUseBatchMode && CanUseBatchMode;
        
        /// <summary>
        ///     Extra stuff to do before executing
        /// </summary>
        /// <exception cref="ExecutionException"></exception>
        protected virtual void SetExecutionInfo() { }

        /// <summary>
        ///     Add stuff to the command line
        /// </summary>
        protected virtual void AppendProgressParameters(StringBuilder sb) {
            if (!string.IsNullOrEmpty(_tempInifilePath)) {
                sb.Append($" -ininame {_tempInifilePath.Quoter()} -basekey {"INI".Quoter()}");
            }
        }

        /// <summary>
        ///     Allows to clean the temporary directories
        /// </summary>
        public virtual void Clean() {
            try {
                _process?.Close();

                // delete temp dir
                if (_localTempDir != null) {
                    Utils.DeleteDirectoryIfExists(_localTempDir, true);
                }

                // restore splashscreen
                RestoreSplashScreen();
            } catch (Exception e) {
                AddHandledExceptions(e);
            }
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
        ///     Start the prowin process with the options defined in this object
        /// </summary>
        protected virtual void StartProcess() {
            var pInfo = new ProcessStartInfo {
                FileName = Env.ProExePath,
                Arguments = _exeParameters.ToString(),
                UseShellExecute = false,
                WorkingDirectory = _processStartDir
            };
            if (UseBatchMode || SilentExecution) {
                pInfo.WindowStyle = ProcessWindowStyle.Hidden;
                pInfo.CreateNoWindow = true;
            }
            if (UseBatchMode) {
                pInfo.RedirectStandardError = true;
                pInfo.RedirectStandardOutput = true;
            }
            _process = new Process {
                StartInfo = pInfo,
                EnableRaisingEvents = true
            };
            if (UseBatchMode) {
                _batchModeOutput = new StringBuilder();
                _process.OutputDataReceived += ProcessOnOutputDataReceived;
                _process.ErrorDataReceived += ProcessOnOutputDataReceived;
            }
            _process.Exited += ProcessOnExited; 
            _process.Start();
            
#if WINDOWSONLYBUILD
            
            if (UseBatchMode && SilentExecution) {
                // on windows, we try to hide the window
                while (!_process.HasExited && _process.TotalProcessorTime.TotalMilliseconds < 1000 && !HideProwinProcess(_process.Id)) { }
            }
#endif
        }

        private void ProcessOnOutputDataReceived(object sender, DataReceivedEventArgs e) {
            _batchModeOutput.AppendLine(e.Data);
        }

        /// <summary>
        ///     Called by the process's thread when it is over, execute the ProcessOnExited event
        /// </summary>
        private void ProcessOnExited(object sender, EventArgs eventArgs) {
            try {
                if (_process.ExitCode > 0) {
                    AddHandledExceptions(new ExecutionProcessException($"An error has occurred during the execution : {Env.ProExePath} {_exeParameters}, in the directory : {_process.StartInfo.WorkingDirectory}, exit code {_process.ExitCode}{(_batchModeOutput != null ? $". The output was {_batchModeOutput}" : "")}", Env.ProExePath, _exeParameters.ToString(), _process.StartInfo.WorkingDirectory, _batchModeOutput?.ToString()));
                    ExecutionFailed = true;              

                } else if (UseBatchMode && _batchModeOutput.Length == 0 || _batchModeOutput[_batchModeOutput.Length - 1] != '<') {
                    // the standard output didn't end with <, indicating that the procedure ran until the end correctly
                    AddHandledExceptions(new ExecutionProcessException($"An error has occurred during the execution : {Env.ProExePath} {_exeParameters}, in the directory : {_process.StartInfo.WorkingDirectory}, exit code {_process.ExitCode}. The output was {_batchModeOutput}", Env.ProExePath, _exeParameters.ToString(), _process.StartInfo.WorkingDirectory, _batchModeOutput?.ToString()));
                    ExecutionFailed = true;     
                    
                } else if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 0) {
                    // else if the log isn't empty, something went wrong
                    AddHandledExceptions(new ExecutionException($"An error has occurred during the execution : {Utils.ReadAllText(_logPath, Encoding.Default)}"));
                    ExecutionFailed = true;
                }

                // if the db log file exists, then the connect statement failed, warn the user
                if (File.Exists(_dbLogPath) && new FileInfo(_dbLogPath).Length > 0) {
                    AddHandledExceptions(new ExecutionException($"An error has occurred when connecting to the database : {Utils.ReadAllText(_dbLogPath, Encoding.Default)}"));
                    ConnectionFailed = true;
                }
            } catch (Exception e) {
                AddHandledExceptions(e);
                ExecutionFailed = true;
            } finally {
                PublishExecutionEndEvents();
            }
        }

        /// <summary>
        ///     publish the end of execution events
        /// </summary>
        protected virtual void PublishExecutionEndEvents() {
            // end of successful/unsuccessful execution action
            try {
                if (ExecutionFailed || ConnectionFailed && NeedDatabaseConnection) {
                    OnExecutionFailed?.Invoke(this);
                } else {
                    OnExecutionOk?.Invoke(this);
                }
            } catch (Exception e) {
                AddHandledExceptions(e);
            }

            // end of execution action
            try {
                OnExecutionEnd?.Invoke(this);
            } catch (Exception e) {
                AddHandledExceptions(e);
            }
        }

        private void DisableSplashScreen() {
            try {
                File.Move(Path.Combine(Env.DlcPath, "bin", "splashscreen.bmp"), Path.Combine(Env.DlcPath, "bin", "splashscreen-disabled.bmp"));
            } catch (Exception) {
                // if it fails it is not really a problem
            }
        }

        private void RestoreSplashScreen() {
            try {
                File.Move(Path.Combine(Env.DlcPath, "bin", "splashscreen-disabled.bmp"), Path.Combine(Env.DlcPath, "bin", "splashscreen.bmp"));
            } catch (Exception) {
                // if it fails it is not really a problem
            }
        }

        protected void AddHandledExceptions(Exception exception, string customMessage = null) {
            if (HandledExceptions == null) {
                HandledExceptions = new List<ExecutionException>();
            }

            if (customMessage != null) {
                HandledExceptions.Add(new ExecutionException(customMessage, exception));
            } else {
                HandledExceptions.Add(new ExecutionException("Openedge execution exception", exception));
            }
        }
        
        private string ProgramProgressRun => OpenedgeResources.GetOpenedgeAsStringFromResources(@"ProgressRun.p");

#if WINDOWSONLYBUILD
    
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className,  string windowTitle);

        [DllImport("user32.dll", SetLastError=true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        // ReSharper disable once InconsistentNaming
        private const int GWL_EX_STYLE = -20;

        // ReSharper disable once InconsistentNaming
        private const int WS_EX_APPWINDOW = 0x00040000;

        // ReSharper disable once InconsistentNaming
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private bool HideProwinProcess(int procId) {
            var hWnd = IntPtr.Zero;
            do {
                hWnd = FindWindowEx(IntPtr.Zero, hWnd, OeConstants.ProwinWindowClass, null);
                GetWindowThreadProcessId(hWnd, out var hWndProcessId);
                if (hWndProcessId == procId) {
                    SetWindowLong(hWnd, GWL_EX_STYLE, (GetWindowLong(hWnd, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
                    ShowWindow(hWnd, 0);
                    return true;
                }
            } while(hWnd != IntPtr.Zero);	
            return false;
        }

#endif
    }
}