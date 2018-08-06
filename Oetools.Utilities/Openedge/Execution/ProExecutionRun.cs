using System.IO;
using System.Linq;

namespace Oetools.Utilities.Openedge.Execution {

    internal class ProExecutionRun : ProExecutionHandleCompilation {

        public ProExecutionRun(IEnvExecutionCompilation env) : base(env) { }
        
        public string WorkingDirectory { get; set; }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            _processStartDir = WorkingDirectory ?? Path.GetDirectoryName(FilesToCompile.First().SourcePath) ?? _localTempDir;
        }
    }
}