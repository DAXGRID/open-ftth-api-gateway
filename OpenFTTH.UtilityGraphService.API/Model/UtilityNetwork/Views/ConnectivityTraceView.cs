namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Represents a connectivity view
    /// </summary>
    public record ConnectivityTraceView
    {
        public string? CircuitName { get; }
        public ConnectivityTraceViewHopInfo[] Hops { get; }

        public ConnectivityTraceView(string? circuitName, ConnectivityTraceViewHopInfo[] hops)
        {
            CircuitName = circuitName;
            Hops = hops;
        }
    }
}
