using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.Outage;
using System;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetOutageView : IQuery<Result<OutageViewNode>> 
    {
        public Guid RouteNetworkElementId { get; }
        public Guid? EquipmentId { get; }

        public GetOutageView(Guid routeNetworkElementId, Guid? equipmentId = null)
        {
            RouteNetworkElementId = routeNetworkElementId;
            EquipmentId = equipmentId;
        }
    }
}
