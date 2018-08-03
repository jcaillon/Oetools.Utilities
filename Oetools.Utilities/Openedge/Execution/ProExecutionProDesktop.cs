namespace Oetools.Packager.Core2.Execution {
    internal class ProExecutionProDesktop : ProExecution {
        protected override ExecutionType ExecutionType => ExecutionType.ProDesktop;
        public ProExecutionProDesktop(IEnvExecution env) : base(env) { }
    }
}