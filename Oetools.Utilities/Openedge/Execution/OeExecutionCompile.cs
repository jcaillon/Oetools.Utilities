namespace Oetools.Utilities.Openedge.Execution {
    
    public class OeExecutionCompile : OeExecutionHandleCompilation {
        
        public override bool NeedDatabaseConnection => true;
        
        public OeExecutionCompile(IEnvExecution env) : base(env) {}

    }
}