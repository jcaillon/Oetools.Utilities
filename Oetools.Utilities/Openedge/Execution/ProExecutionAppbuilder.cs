using Oetools.Utilities.Lib.Extension;

namespace Oetools.Packager.Core2.Execution {
    internal class ProExecutionAppbuilder : ProExecution {

        protected override ExecutionType ExecutionType => ExecutionType.Appbuilder;

        public string CurrentFile { get; set; }

        protected override void SetExecutionInfo() {
            base.SetExecutionInfo();
            SetPreprocessedVar("CurrentFilePath", CurrentFile.PreProcQuoter());
        }

        public ProExecutionAppbuilder(IEnvExecution proEnv) : base(proEnv) { }
    }
}