using System.Linq;

namespace Oetools.Packager.Core2.Execution {
    
    internal class ProExecutionGenerateDebugfile : ProExecutionHandleCompilation {

        protected override ExecutionType ExecutionType => ExecutionType.GenerateDebugfile;

        protected override bool SilentExecution => true;
        
        protected override bool CanUseBatchMode => true;
        
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