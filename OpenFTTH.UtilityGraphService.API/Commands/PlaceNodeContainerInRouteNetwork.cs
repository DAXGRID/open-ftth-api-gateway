using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.RouteNetwork.API.Model;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record PlaceNodeContainerInRouteNetwork : BaseCommand, ICommand<Result>
    {
        public Guid NodeContainerId { get; }
        public Guid NodeContainerSpecificationId { get; }
        public RouteNetworkInterest NodeOfInterest { get; }
        public NamingInfo? NamingInfo { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public Guid? ManufacturerId { get; init; }

        public PlaceNodeContainerInRouteNetwork(Guid correlationId, UserContext userContext, Guid nodeContainerId, Guid nodeContainerSpecificationId, RouteNetworkInterest nodeOfInterest) : base(correlationId, userContext)
        {
            NodeContainerId = nodeContainerId;
            NodeContainerSpecificationId = nodeContainerSpecificationId;
            NodeOfInterest = nodeOfInterest;
        }
    }
}
