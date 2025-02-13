using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.Trace;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public record GetEquipmentDetailsResult
    {
        public LookupCollection<SpanEquipmentWithRelatedInfo>? SpanEquipment { get; init; }
        public LookupCollection<TerminalEquipment>? TerminalEquipment { get; init; }
        public LookupCollection<NodeContainer>? NodeContainers { get; init; }
        public LookupCollection<RouteNetworkTraceResult>? RouteNetworkTraces { get; init; }

        public GetEquipmentDetailsResult()
        {
        }
    }
}
