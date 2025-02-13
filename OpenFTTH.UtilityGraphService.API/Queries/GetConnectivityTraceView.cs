using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetConnectivityTraceView : IQuery<Result<ConnectivityTraceView>> 
    {
        public Guid RouteNodeId { get; }
        public Guid TerminalOrSpanSegmentId { get; }

        public GetConnectivityTraceView(Guid routeNodeId, Guid terminalOrSpanSegmentId)
        {
            RouteNodeId = routeNodeId;
            TerminalOrSpanSegmentId = terminalOrSpanSegmentId;
        }
    }
}
