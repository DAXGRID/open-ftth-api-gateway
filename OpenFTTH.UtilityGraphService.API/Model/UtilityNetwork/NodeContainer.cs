using OpenFTTH.Events.Core.Infos;
using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record NodeContainer : IEquipment
    {
        public Guid Id { get; }
        public Guid SpecificationId { get; init; }
        public Guid InterestId { get;  }
        public Guid RouteNodeId { get;  }
        public Guid? ManufacturerId { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public NodeContainerVerticalContentAlignmentEnum VertialContentAlignmemt { get; init; }
        public Rack[]? Racks { get; init; }
        public Guid[]? TerminalEquipmentReferences { get; init; }
        public TerminalToTerminalConnection[]? TerminalToTerminalConnections { get; init; }

        public string? Name => null;
        public string? Description => null;

        public NodeContainer(Guid id, Guid specificationId, Guid interestId, Guid routeNodeId)
        {
            Id = id;
            SpecificationId = specificationId;
            InterestId = interestId;
            RouteNodeId = routeNodeId;
        }
    }
}
