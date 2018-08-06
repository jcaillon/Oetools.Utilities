namespace Oetools.Builder.Core2.Execution {
    internal class ProExecutionDictionary : ProExecution {
        protected override ExecutionType ExecutionType => ExecutionType.Dictionary;
        public ProExecutionDictionary(IEnvExecution env) : base(env) { }
    }
}