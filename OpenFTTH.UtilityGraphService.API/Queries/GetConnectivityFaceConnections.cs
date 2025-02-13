using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetConnectivityFaceConnections : IQuery<Result<List<ConnectivityFaceConnection>>> 
    { 
        public Guid routeNodeId { get; }

        public Guid spanOrTerminalEquipmentId { get; }

        public FaceKindEnum FaceType { get; set; }

        public GetConnectivityFaceConnections(Guid routeNodeId, Guid spanOrTerminalEquipmentId, FaceKindEnum faceType)
        {
            this.routeNodeId = routeNodeId;
            this.spanOrTerminalEquipmentId = spanOrTerminalEquipmentId;
            FaceType = faceType;
        }
    }
}
