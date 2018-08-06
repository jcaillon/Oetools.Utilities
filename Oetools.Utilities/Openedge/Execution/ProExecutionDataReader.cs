namespace Oetools.Builder.Core2.Execution {
    internal class ProExecutionDataReader : ProExecutionDataDigger {

        protected override ExecutionType ExecutionType => ExecutionType.DataReader;

        public ProExecutionDataReader(IEnvExecution env, string dataDiggerFolder) : base(env, dataDiggerFolder) { }
    }
}