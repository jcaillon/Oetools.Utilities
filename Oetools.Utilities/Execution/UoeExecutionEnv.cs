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
using DotUtilities;
using DotUtilities.Extensions;
using Oetools.Utilities.Openedge.Database;

namespace Oetools.Utilities.Openedge.Execution {

    /// <inheritdoc cref="AUoeExecutionEnv" />
    public class UoeExecutionEnv : AUoeExecutionEnv, IDisposable {

        private string _iniFilePath;
        private string _tempIniFilePath;
        private bool? _canProVersionUseNoSplash;
        private string _dlcDirectoryPath;
        private string _tempDirectory;
        private IEnumerable<UoeDatabaseConnection> _databaseConnections;
        private Version _proVersion;
        private Dictionary<string, string> _tablesCrc;
        private HashSet<string> _sequences;
        private Encoding _ioEncoding;
        private UoeProcessArgs _defaultStartUpArguments;
        private UoeProcessArgs _proExeCommandLineParameters;

        /// <inheritdoc />
        public override string DlcDirectoryPath {
            get => _dlcDirectoryPath ?? (_dlcDirectoryPath = UoeUtilities.GetDlcPathFromEnv());
            set {
                _canProVersionUseNoSplash = null;
                _proVersion = null;
                _dlcDirectoryPath = value;
            }
        }

        /// <inheritdoc />
        public override bool UseProgressCharacterMode { get; set; }

        /// <summary>
        /// Should we add the max try parameter to each connection string?
        /// </summary>
        public bool DatabaseConnectionStringAppendMaxTryOne { get; set; } = true;

        /// <inheritdoc />
        public override IEnumerable<UoeDatabaseConnection> DatabaseConnections {
            get {
                if (DatabaseConnectionStringAppendMaxTryOne) {
                    foreach (var uoeDatabaseConnection in _databaseConnections.ToNonNullEnumerable()) {
                        uoeDatabaseConnection.MaxConnectionTry = 1;
                    }
                }
                return _databaseConnections;
            }
            set => _databaseConnections = value;
        }

        /// <inheritdoc />
        public override IEnumerable<IUoeExecutionDatabaseAlias> DatabaseAliases { get; set; }

        /// <inheritdoc />
        public override string IniFilePath {
            get {
                if (_tempIniFilePath == null) {
                    if (!string.IsNullOrEmpty(_iniFilePath) && File.Exists(_iniFilePath)) {
                        _tempIniFilePath = Path.Combine(TempDirectory, $"ini_{Path.GetRandomFileName()}.ini");

                        // we need to copy the .ini but we must delete the PROPATH= part, as stupid as it sounds, if we leave a huge PROPATH
                        // in this file, it increases the compilation time by a stupid amount... unbelievable i know, but trust me, it does...
                        var fileContent = Utils.ReadAllText(_iniFilePath, IoEncoding);
                        var regex = new Regex("^PROPATH=.*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                        fileContent = regex.Replace(fileContent, @"PROPATH=");
                        File.WriteAllText(_tempIniFilePath, fileContent, IoEncoding);
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

        /// <inheritdoc />
        public override List<string> ProPathList { get; set; }

        /// <inheritdoc />
        public override UoeProcessArgs ProExeCommandLineParameters {
            get => _proExeCommandLineParameters;
            set {
                _ioEncoding = null;
                _proExeCommandLineParameters = value;
            }
        }

        /// <inheritdoc />
        public override string PreExecutionProgramPath { get; set; }

        /// <inheritdoc />
        public override string PostExecutionProgramPath { get; set; }

        /// <inheritdoc />
        public override bool CanProVersionUseNoSplash {
            get {
                if (!_canProVersionUseNoSplash.HasValue) {
                    _canProVersionUseNoSplash = UoeUtilities.CanProVersionUseNoSplashParameter(ProVersion);
                }
                return _canProVersionUseNoSplash.Value;
            }
        }

        /// <inheritdoc />
        public override string TempDirectory {
            get => _tempDirectory ?? (_tempDirectory = Utils.CreateTempDirectory(Utils.GetRandomName()));
            set {
                _tempIniFilePath = null;
                _tempDirectory = value;
            }
        }

        /// <inheritdoc />
        public override bool TryToHideProcessFromTaskBarOnWindows { get; set; } = true;

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

        /// <inheritdoc />
        public override bool IsProVersionHigherOrEqualTo(Version version) {
            return ProVersion != null && version != null && ProVersion.CompareTo(version) >= 0;
        }

        /// <inheritdoc />
        public override Encoding IoEncoding {
            set => _ioEncoding = value;
            get {
                if (_defaultStartUpArguments == null) {
                    _defaultStartUpArguments = UoeUtilities.GetOpenedgeDefaultStartupArgs(DlcDirectoryPath);
                }
                if (_ioEncoding == null) {
                    var codePageName = UoeUtilities.GetProcessIoCodePageFromArgs(new UoeProcessArgs().Append(_defaultStartUpArguments).Append(ProExeCommandLineParameters) as UoeProcessArgs);
                    UoeUtilities.GetEncodingFromOpenedgeCodePage(codePageName, out _ioEncoding);
                }
                return _ioEncoding;
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

        protected virtual void InitDatabasesInfo() {
            using (var exec = new UoeExecutionDbExtractTableCrcAndSequenceList(this)) {
                exec.ExecuteNoWait();
                exec.WaitForExit();
                _tablesCrc = exec.TablesCrc;
                _sequences = exec.Sequences;
            }
        }

        public void Dispose() {
            Utils.DeleteDirectoryIfExists(TempDirectory, true);
        }
    }
}
