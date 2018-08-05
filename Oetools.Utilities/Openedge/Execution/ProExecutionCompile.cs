using System.IO;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Packager.Core2.Execution {
    
    public class ProExecutionCompile : ProExecutionHandleCompilation {
        
        protected override ExecutionType ExecutionType => ExecutionType.Compile;

        protected override bool SilentExecution => true;
        
        public ProExecutionCompile(IEnvExecutionCompilation env) : base(env) {}

    }
}