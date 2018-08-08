using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {

    internal class OeExecutionRun : OeExecutionHandleCompilation {
        
        public string FullClientLogPath { get; set; }

        public OeExecutionRun(IEnvExecution env) : base(env) { }

        protected override bool SilentExecution => false;

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            WorkingDirectory = WorkingDirectory ?? Path.GetDirectoryName(FilesToCompile.First().SourcePath);
            
            SetPreprocessedVar("RunProgramMode", true.ToString());
            SetPreprocessedVar("RunFullClientLogPath", FullClientLogPath.ProPreProcStringify());
        }
    }
}