namespace Oetools.Packager.Core2.Execution {
    internal class ProExecutionCheckSyntax : ProExecutionHandleCompilation {

        protected override ExecutionType ExecutionType => ExecutionType.CheckSyntax;

        protected override bool SilentExecution => true;
        
        protected override bool CanUseBatchMode => true;
        
        public ProExecutionCheckSyntax(IEnvExecutionCompilation env) : base(env) { }
    }
}