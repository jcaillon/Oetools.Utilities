using System.IO;
using System.Linq;

namespace Oetools.Packager.Core2.Execution {

    internal class ProExecutionRun : ProExecutionHandleCompilation {

        public ProExecutionRun(IEnvExecutionCompilation env) : base(env) { }
        
        protected override ExecutionType ExecutionType => ExecutionType.Run;

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            _processStartDir = Path.GetDirectoryName(FilesToCompile.First().SourcePath) ?? _localTempDir;
        }
    }
}