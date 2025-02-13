using OpenFTTH.Core;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.Events.RouteNetwork.Infos;
using System;

namespace OpenFTTH.RouteNetwork.API.Model
{
    /// <summary>
    /// Data transformer object holding either route node and route segment information.
    /// </summary>
    public record RouteNetworkElement : IIdentifiedObject
    {
        public Guid Id { get; }
        public RouteNetworkElementKindEnum Kind { get; }
        public string? Coordinates { get; init; }
        public RouteNodeInfo? RouteNodeInfo { get; init; }
        public RouteSegmentInfo? RouteSegmentInfo { get; init; }
        public NamingInfo? NamingInfo { get; init; }
        public LifecycleInfo? LifecycleInfo { get; init; }
        public SafetyInfo? SafetyInfo { get; init; }
        public MappingInfo? MappingInfo { get; init; }
        public RouteNetworkElementInterestRelation[]? InterestRelations { get; set; }

        public string? Name => NamingInfo?.Name;

        public string? Description => NamingInfo?.Description;

        public RouteNetworkElement(Guid id, RouteNetworkElementKindEnum kind)
        {
            this.Id = id;
            this.Kind = kind;
        }
    }
}
