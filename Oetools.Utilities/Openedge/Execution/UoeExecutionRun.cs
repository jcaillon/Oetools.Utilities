#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionRun.cs) is part of Oetools.Utilities.
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

using System.IO;
using System.Linq;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution.Exceptions;

namespace Oetools.Utilities.Openedge.Execution {

    internal class UoeExecutionRun : UoeExecutionHandleCompilation {

        public bool RunSilently { get; set; }
        
        public string FullClientLogPath { get; set; }

        public string LogEntryTypes { get; set; } = "4GLMessages,4GLTrace,4GLTrans,DB.Connects,FileID,QryInfo,ProEvents.UI.Char,ProEvents.UI.Command,ProEvents.Other,DynObjects.Class,DynObjects.DB,DynObjects.XML,DynObjects.Other,DynObjects.UI,DS.Cursor,DS.QryInfo,IgnoredOps,SAX";

        private readonly string _filePathToRun;

        public UoeExecutionRun(IUoeExecutionEnv env, string filePathToRun) : base(env) {
            _filePathToRun = filePathToRun;
        }

        protected override void CheckParameters() {
            if (string.IsNullOrEmpty(_filePathToRun)) {
                throw new UoeExecutionParametersException("The path of the file to run is empty or null");
            }
            FilesToCompile = new PathList<UoeFileToCompile> {
                new UoeFileToCompile(_filePathToRun)
            };
            base.CheckParameters();
        }

        protected override bool SilentExecution => RunSilently;

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            WorkingDirectory = WorkingDirectory ?? Path.GetDirectoryName(FilesToCompile.First().Path);
            
            SetPreprocessedVar("RunProgramMode", true.ToString());
            SetPreprocessedVar("RunFullClientLogPath", FullClientLogPath.ProPreProcStringify());
            SetPreprocessedVar("LogEntryTypes", LogEntryTypes.ProPreProcStringify());
        }
    }
}