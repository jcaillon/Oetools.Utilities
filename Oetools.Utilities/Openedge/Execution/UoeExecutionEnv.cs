#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionEnv.cs) is part of Oetools.Utilities.
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
    
    public class UoeExecutionEnv : AUoeExecutionEnv {
        
        private string _iniFilePath;
        private string _tempIniFilePath;
        private bool? _canProVersionUseNoSplash;
        private string _dlcDirectoryPath;
        private string _tempDirectory;
        private string _databaseConnectionString;
        private Version _proVersion;

        /// <inheritdoc cref="AUoeExecutionEnv.DlcDirectoryPath"/>
        public override string DlcDirectoryPath {
            get => _dlcDirectoryPath ?? (_dlcDirectoryPath = UoeUtilities.GetDlcPathFromEnv());
            set {
                _canProVersionUseNoSplash = null;
                _proVersion = null;
                _dlcDirectoryPath = value;
            }
        }

        /// <inheritdoc cref="AUoeExecutionEnv.UseProgressCharacterMode"/>
        public override bool UseProgressCharacterMode { get; set; }

        /// <summary>
        /// Should we add the max try parameter to the connection string?
        /// </summary>
        public bool DatabaseConnectionStringAppendMaxTryOne { get; set; } = true;
        
        /// <inheritdoc cref="AUoeExecutionEnv.DatabaseConnectionString"/>
        public override string DatabaseConnectionString {
            get => DatabaseConnectionStringAppendMaxTryOne ? _databaseConnectionString?.Replace("-db", "-ct 1 -db") : _databaseConnectionString;
            set => _databaseConnectionString = value.CliCompactWhitespaces();
        }

        /// <inheritdoc cref="AUoeExecutionEnv.DatabaseAliases"/>
        public override IEnumerable<AUoeExecutionDatabaseAlias> DatabaseAliases { get; set; }
        
        /// <inheritdoc cref="AUoeExecutionEnv.IniFilePath"/>
        public override string IniFilePath {
            get {
                if (_tempIniFilePath == null) {
                    if (!string.IsNullOrEmpty(_iniFilePath) && File.Exists(_iniFilePath)) {
                        _tempIniFilePath = Path.Combine(TempDirectory, $"ini_{DateTime.Now:HHmmssfff}_{Path.GetRandomFileName()}.ini");

                        // we need to copy the .ini but we must delete the PROPATH= part, as stupid as it sounds, if we leave a huge PROPATH 
                        // in this file, it increases the compilation time by a stupid amount... unbelievable i know, but trust me, it does...
                        var fileContent = Utils.ReadAllText(_iniFilePath, GetIoEncoding());
                        var regex = new Regex("^PROPATH=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                        fileContent = regex.Replace(fileContent, @"PROPATH=");
                        File.WriteAllText(_tempIniFilePath, fileContent, GetIoEncoding());
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
        
        /// <inheritdoc cref="AUoeExecutionEnv.ProPathList"/>
        public override List<string> ProPathList { get; set; }
        
        /// <inheritdoc cref="AUoeExecutionEnv.ProExeCommandLineParameters"/>
        public override string ProExeCommandLineParameters { get; set; }
        
        /// <inheritdoc cref="AUoeExecutionEnv.PreExecutionProgramPath"/>
        public override string PreExecutionProgramPath { get; set; }
        
        /// <inheritdoc cref="AUoeExecutionEnv.PostExecutionProgramPath"/>
        public override string PostExecutionProgramPath { get; set; }

        /// <inheritdoc cref="AUoeExecutionEnv.CanProVersionUseNoSplash"/>
        public override bool CanProVersionUseNoSplash {
            get {
                if (!_canProVersionUseNoSplash.HasValue) {
                    _canProVersionUseNoSplash = UoeUtilities.CanProVersionUseNoSplashParameter(ProVersion);
                }
                return _canProVersionUseNoSplash.Value;
            }
        }

        /// <inheritdoc cref="AUoeExecutionEnv.TempDirectory"/>
        public override string TempDirectory {
            get => _tempDirectory ?? Utils.GetTempDirectory();
            set {
                _tempIniFilePath = null;
                _tempDirectory = value;
            }
        }

        /// <summary>
        /// Returns the installed version of openedge.
        /// </summary>
        public Version ProVersion {
            get {
                if (_proVersion == null) {
                    _proVersion = UoeUtilities.GetProVersionFromDlc(DlcDirectoryPath);
                }
                return _proVersion;
            }
        }
        
        /// <inheritdoc cref="AUoeExecutionEnv.IsProVersionHigherOrEqualTo"/>
        public override bool IsProVersionHigherOrEqualTo(Version version) {
            return ProVersion != null && version != null && ProVersion.CompareTo(version) >= 0;
        }

        /// <inheritdoc cref="AUoeExecutionEnv.GetIoEncoding"/>
        public override Encoding GetIoEncoding() {
            if (_ioEncoding == null) {
                if (string.IsNullOrEmpty(CodePageName)) {
                    _codePageName = Utils.IsRuntimeWindowsPlatform ? UoeUtilities.GetGuiCodePageFromDlc(DlcDirectoryPath) : UoeUtilities.GetIoCodePageFromDlc(DlcDirectoryPath);
                }

                UoeUtilities.GetEncodingFromOpenedgeCodePage(CodePageName, out _ioEncoding);
            }
            return _ioEncoding;
        }

        /// <summary>
        /// The code page to use for i/o with openedge processes.
        /// Defaults to the one read in $DLC/startup.pf.
        /// </summary>
        public string CodePageName {
            get => _codePageName;
            set {
                _ioEncoding = null;
                _codePageName = value;
            }
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
        private Encoding _ioEncoding;
        private string _codePageName;

        private void InitDatabasesInfo() {
            using (var exec = new UoeExecutionDbExtractTableAndSequenceList(this)) {
                exec.Start();
                exec.WaitForExecutionEnd();
                _tablesCrc = exec.TablesCrc;
                _sequences = exec.Sequences;
            }
        }
    }
}