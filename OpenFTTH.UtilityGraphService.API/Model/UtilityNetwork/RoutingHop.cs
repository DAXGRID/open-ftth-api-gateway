using OpenFTTH.RouteNetwork.API.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    /// <summary>
    /// Used when placing/routing span equipment inside other span equipment and/or the route network
    /// </summary>
    public record RoutingHop
    {
        public RoutingHopKind Kind { get; }
        public Guid? StartRouteNode { get; }
        public Guid? StartSpanEquipmentId { get; }
        public int? StartStrutureIndex { get; }
        public Guid? StartSpanSegmentId { get; }
        public Guid[]? WalkOfinterest { get; }

        /// <summary>
        /// Route through a span equipment by structure index
        /// </summary>
        public RoutingHop(Guid startRouteNodeId, Guid startSpanEquipmentId, int startStructureIndex)
        {
            Kind = RoutingHopKind.RouteThroughSpanEquipmentByStructureIndex;
            StartRouteNode = startRouteNodeId;
            StartSpanEquipmentId = startSpanEquipmentId;
            StartStrutureIndex = startStructureIndex;
        }

        /// <summary>
        /// Route through a span equipment by span segment id
        /// </summary>
        public RoutingHop(Guid startRouteNodeId, Guid startSpanSegmentId)
        {
            Kind = RoutingHopKind.RouteThroughSpanEquipmentBySpanSegmentId;
            StartRouteNode = startRouteNodeId;
            StartSpanSegmentId = startSpanSegmentId;
        }

        /// <summary>
        /// Route through the route network
        /// </summary>
        public RoutingHop(Guid[] walkOfinterest)
        {
            Kind = RoutingHopKind.RouteThroughRouteNetwork;
            WalkOfinterest = walkOfinterest;
        }
    }
}
