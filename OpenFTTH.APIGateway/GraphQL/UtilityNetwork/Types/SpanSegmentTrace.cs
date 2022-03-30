using System;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Types
{
    public class SpanSegmentTrace
    {
        public Guid[] RouteNetworkSegmentIds { get; set; }
        public string[] RouteNetworkSegmentGeometries { get; set; }
        public Guid[] RouteNetworkNodeIds { get; set; }
    }
}
