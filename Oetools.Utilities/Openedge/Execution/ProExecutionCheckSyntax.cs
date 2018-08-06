namespace Oetools.Builder.Core2.Execution {
    internal class ProExecutionCheckSyntax : ProExecutionHandleCompilation {

        protected override ExecutionType ExecutionType => ExecutionType.CheckSyntax;

        protected override bool SilentExecution => true;
        
        public ProExecutionCheckSyntax(IEnvExecutionCompilation env) : base(env) { }
    }
}