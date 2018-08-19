#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (EnvExecution.cs) is part of Oetools.Utilities.
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
using System.Text;
using System.Text.RegularExpressions;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {
    
    public class EnvExecution : IEnvExecution {
        
        public EnvExecution() {
            DlcDirectoryPath = ProUtilities.GetDlcPathFromEnv();
        }

        private string _iniFilePath;
        private string _tempIniFilePath;
        private bool? _canProVersionUseNoSplash;
        private string _dlcDirectoryPath;
        private string _tempDirectory;
        private string _databaseConnectionString;
        private Version _proVersion;

        public string DlcDirectoryPath {
            get => _dlcDirectoryPath;
            set {
                _canProVersionUseNoSplash = null;
                _proVersion = null;
                _dlcDirectoryPath = value;
            }
        }

        public bool UseProgressCharacterMode { get; set; }

        public bool DatabaseConnectionStringAppendMaxTryOne { get; set; } = true;
        
        public string DatabaseConnectionString {
            get {
                return DatabaseConnectionStringAppendMaxTryOne ? _databaseConnectionString?.Replace("-db", "-ct 1 -db") : _databaseConnectionString;
            }
            set => _databaseConnectionString = value.CliCompactWhitespaces();
        }

        public IEnumerable<IEnvExecutionDatabaseAlias> DatabaseAliases { get; set; }
        
        public string IniFilePath {
            get {
                if (_tempIniFilePath == null) {
                    if (!string.IsNullOrEmpty(_iniFilePath) && File.Exists(_iniFilePath)) {
                        _tempIniFilePath = Path.Combine(TempDirectory, $"ini_{DateTime.Now:HHmmssfff}_{Path.GetRandomFileName()}.ini");

                        // we need to copy the .ini but we must delete the PROPATH= part, as stupid as it sounds, if we leave a huge PROPATH 
                        // in this file, it increases the compilation time by a stupid amount... unbelievable i know, but trust me, it does...
                        var fileContent = Utils.ReadAllText(_iniFilePath, Encoding.Default);
                        var regex = new Regex("^PROPATH=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                        fileContent = regex.Replace(fileContent, @"PROPATH=");
                        File.WriteAllText(_tempIniFilePath, fileContent, Encoding.Default);
                    } else {
                        return _iniFilePath;
                    }
                }
                return _tempIniFilePath;
            }
            set {
                if (!string.IsNullOrEmpty(_tempIniFilePath)) {
                    Utils.DeleteFileIfNeeded(_tempIniFilePath);
                }
                _tempIniFilePath = null;
                _iniFilePath = value;
            }
        }
        
        public List<string> ProPathList { get; set; }
        public string ProExeCommandLineParameters { get; set; }
        public string PreExecutionProgramPath { get; set; }
        public string PostExecutionProgramPath { get; set; }

        public bool CanProVersionUseNoSplash {
            get {
                if (!_canProVersionUseNoSplash.HasValue) {
                    _canProVersionUseNoSplash = ProUtilities.CanProVersionUseNoSplashParameter(ProVersion);
                }
                return _canProVersionUseNoSplash.Value;
            }
        }

        public string TempDirectory {
            get => _tempDirectory ?? Utils.GetTempDirectory();
            set {
                _tempIniFilePath = null;
                _tempDirectory = value;
            }
        }

        public Version ProVersion {
            get {
                if (_proVersion == null) {
                    _proVersion = ProUtilities.GetProVersionFromDlc(DlcDirectoryPath);
                }
                return _proVersion;
            }
        }
        
        public virtual bool IsProVersionHigherOrEqualTo(Version version) {
            return ProVersion != null && version != null && ProVersion.CompareTo(version) >= 0;
        }
        
        public virtual Dictionary<string, string> TablesCrc {
            get {
                if (_tablesCrc == null) {
                    InitDatabasesInfo();
                }
                return _tablesCrc;
            }
        }

        public virtual HashSet<string> Sequences {
            get {
                if (_sequences == null) {
                    InitDatabasesInfo();
                }
                return _sequences;
            }
        }
        
        private Dictionary<string, string> _tablesCrc;
        
        private HashSet<string> _sequences;
        
        private void InitDatabasesInfo() {
            using (var exec = new OeExecutionDbExtractTableAndSequenceList(this)) {
                exec.Start();
                exec.WaitForExecutionEnd();
                _tablesCrc = exec.TablesCrc;
                _sequences = exec.Sequences;
            }
        }
    }
}