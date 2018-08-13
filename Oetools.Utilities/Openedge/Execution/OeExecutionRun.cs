using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {

    internal class OeExecutionRun : OeExecutionHandleCompilation {

        public bool RunSilently { get; set; } = false;
        
        public string FullClientLogPath { get; set; }

        public string LogEntryTypes { get; set; } = "4GLMessages,4GLTrace,4GLTrans,DB.Connects,FileID,QryInfo,ProEvents.UI.Char,ProEvents.UI.Command,ProEvents.Other,DynObjects.Class,DynObjects.DB,DynObjects.XML,DynObjects.Other,DynObjects.UI,DS.Cursor,DS.QryInfo,IgnoredOps,SAX";

        private readonly string _filePathToRun;

        public OeExecutionRun(IEnvExecution env, string filePathToRun) : base(env) {
            _filePathToRun = filePathToRun;
        }

        protected override void CheckParameters() {
            if (string.IsNullOrEmpty(_filePathToRun)) {
                throw new ExecutionParametersException("The path of the file to run is empty or null");
            }
            FilesToCompile = new List<FileToCompile> { new FileToCompile(_filePathToRun) };
            base.CheckParameters();
        }

        protected override bool SilentExecution => RunSilently;

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            WorkingDirectory = WorkingDirectory ?? Path.GetDirectoryName(FilesToCompile.First().SourcePath);
            
            SetPreprocessedVar("RunProgramMode", true.ToString());
            SetPreprocessedVar("RunFullClientLogPath", FullClientLogPath.ProPreProcStringify());
            SetPreprocessedVar("LogEntryTypes", LogEntryTypes.ProPreProcStringify());
        }
    }
}