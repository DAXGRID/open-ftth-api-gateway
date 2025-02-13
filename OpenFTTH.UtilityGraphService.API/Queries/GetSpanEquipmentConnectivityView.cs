using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetSpanEquipmentConnectivityView : IQuery<Result<SpanEquipmentAZConnectivityViewModel>> 
    { 
        public Guid RouteNetworkElementId { get; }
        public Guid[] SpanEquipmentOrSegmentIds { get; }

        public GetSpanEquipmentConnectivityView(Guid routeNetworkElementId, Guid[] spanEquipmentOrSegmentIds)
        {
            RouteNetworkElementId = routeNetworkElementId;
            SpanEquipmentOrSegmentIds = spanEquipmentOrSegmentIds;
        }
    }
}
