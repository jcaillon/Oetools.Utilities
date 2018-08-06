namespace Oetools.Utilities.Openedge.Execution {
    
    public class ProExecutionCompile : ProExecutionHandleCompilation {
        
        protected override bool SilentExecution => true;
        
        public ProExecutionCompile(IEnvExecutionCompilation env) : base(env) {}

    }
}