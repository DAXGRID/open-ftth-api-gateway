using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetTerminalEquipmentConnectivityView : IQuery<Result<TerminalEquipmentAZConnectivityViewModel>> 
    { 
        public Guid routeNodeId { get; }
        public Guid terminalEquipmentOrRackId { get; }

        public GetTerminalEquipmentConnectivityView(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            this.routeNodeId = routeNodeId;
            this.terminalEquipmentOrRackId = terminalEquipmentOrRackId;
        }
    }
}
