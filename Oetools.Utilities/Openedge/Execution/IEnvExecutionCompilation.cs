namespace Oetools.Utilities.Openedge.Execution {
    
    public interface IEnvExecutionCompilation : IEnvExecution {
        
        /// <summary>
        ///     When true, we activate the log just before compiling with FileId active + we generate a file that list referenced
        ///     table in the .r
        /// </summary>
        bool IsAnalysisMode { get; }

        bool CompileWithDebugList { get; }
        bool CompileWithXref { get; }
        bool CompileWithListing { get; }
        bool CompileUseXmlXref { get; }
       
    }
}