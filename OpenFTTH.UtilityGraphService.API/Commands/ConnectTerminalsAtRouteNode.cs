using FluentResults;
using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record ConnectTerminalsAtRouteNode : BaseCommand, ICommand<Result>
    {
        public Guid RouteNodeId { get; }
        public Guid FromTerminalId { get; }
        public Guid ToTerminalId { get; }
        public double FiberCoordLength { get; }

        public ConnectTerminalsAtRouteNode(Guid correlationId, UserContext userContext, Guid routeNodeId, Guid fromTerminalId, Guid toTerminalId, double fiberCoordLength) : base(correlationId, userContext)
        {
            RouteNodeId = routeNodeId;
            FromTerminalId = fromTerminalId;
            ToTerminalId = toTerminalId;
            FiberCoordLength = fiberCoordLength;
        }
    }
}
