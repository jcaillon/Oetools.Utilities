using System.IO;
using System.Linq;
using System.Text;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {

    internal class OeExecutionRun : OeExecutionHandleCompilation {

        public OeExecutionRun(IEnvExecution env) : base(env) { }

        protected override bool SilentExecution => false;

        public string WorkingDirectory { get; set; }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            _processStartDir = WorkingDirectory ?? Path.GetDirectoryName(FilesToCompile.First().SourcePath) ?? _tempDir;
        }

        protected override void AppendProgressParameters(StringBuilder sb) {
            base.AppendProgressParameters(sb);
            sb.Append($" -T {_tempDir}");
        }
    }
}