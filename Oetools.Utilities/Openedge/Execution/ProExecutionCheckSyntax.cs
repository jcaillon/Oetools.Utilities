namespace Oetools.Utilities.Openedge.Execution {
    internal class ProExecutionCheckSyntax : ProExecutionHandleCompilation {

        protected override bool SilentExecution => true;
        
        public ProExecutionCheckSyntax(IEnvExecutionCompilation env) : base(env) { }
    }
}