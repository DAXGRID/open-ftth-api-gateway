using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetSpanEquipmentPassageView : IQuery<Result<SpanEquipmentPassageViewModel>> 
    { 
        public Guid RouteNetworkElementId { get; }
        public Guid[] SpanEquipmentOrSegmentIds { get; }

        public GetSpanEquipmentPassageView(Guid routeNetworkElementId, Guid[] spanEquipmentOrSegmentIds)
        {
            RouteNetworkElementId = routeNetworkElementId;
            SpanEquipmentOrSegmentIds = spanEquipmentOrSegmentIds;
        }
    }
}
