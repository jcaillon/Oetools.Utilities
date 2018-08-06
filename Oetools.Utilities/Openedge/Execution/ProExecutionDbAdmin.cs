namespace Oetools.Builder.Core2.Execution {
    internal class ProExecutionDbAdmin : ProExecution {
        protected override ExecutionType ExecutionType => ExecutionType.DbAdmin;
        public ProExecutionDbAdmin(IEnvExecution env) : base(env) { }
    }
}