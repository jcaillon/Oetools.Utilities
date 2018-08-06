using System.Linq;

namespace Oetools.Utilities.Openedge.Execution {
    
    internal class ProExecutionGenerateDebugfile : ProExecutionHandleCompilation {

        protected override bool SilentExecution => true;
        
        public ProExecutionGenerateDebugfile(IEnvExecutionCompilation env) : base(env) { }

        public string GeneratedFilePath {
            get {
                if (Env.CompileWithListing)
                    return CompiledFiles.First().CompOutputLis;
                if (Env.CompileWithXref)
                    return CompiledFiles.First().CompOutputXrf;
                return CompiledFiles.First().CompOutputDbg;
            }
        }

    }
}