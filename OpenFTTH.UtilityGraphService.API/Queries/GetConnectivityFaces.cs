using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetConnectivityFaces : IQuery<Result<List<ConnectivityFace>>> 
    { 
        public Guid routeNodeId { get; }

        public GetConnectivityFaces(Guid routeNodeId)
        {
            this.routeNodeId = routeNodeId;
        }
    }
}
