using DAX.ObjectVersioning.Graph;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.Graph.Trace;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.QueryHandlers.PassageView
{
    public class CablePassageViewBuilder : PassageViewBuilder
    {
        List<UtilityNetworkHop> _networkHops = new();

        HashSet<Guid> _woiContainsId = new();
       

        public CablePassageViewBuilder(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, Guid routeNetworkElementId, SpanEquipment cableSpanEquipment) : base(eventStore, utilityNetwork, queryDispatcher, routeNetworkElementId, cableSpanEquipment)
        {
            if (!_spanEquipment.IsCable)
                throw new ApplicationException("CablePassageViewBuilder can only be used on cable span equipments");
        }

        public SpanEquipmentPassageViewEquipmentInfo GetCablePassageView()
        {
            var walkOfInterest = _routeNetworkInterests[_spanEquipment.WalkOfInterestId];
         
            // Build woi index
            for (int index = 0; index < walkOfInterest.RouteNetworkElementRefs.Count; index++)
            {
                _woiContainsId.Add(walkOfInterest.RouteNetworkElementRefs[index]);
            }

            // Build utility network hop index
            if (_spanEquipment.UtilityNetworkHops != null)
            {
                foreach (var hop in _spanEquipment.UtilityNetworkHops)
                    _networkHops.Add(hop);
            }

            return new SpanEquipmentPassageViewEquipmentInfo(
                id: _spanEquipment.Id,
                category: _spanEquipmentSpecification.Category,
                name: GetName(),
                specName: _spanEquipmentSpecification.Name,
                lines: BuildCableLineInfos().ToArray()
            );
        }

        public List<SpanEquipmentPassageViewLineInfo> BuildCableLineInfos()
        {
            List<SpanEquipmentPassageViewLineInfo> lineInfos = new();

            int hopNumber = 1;

            foreach (var utilityHop in _networkHops)
            {
                if (!_woiContainsId.Contains(utilityHop.FromNodeId))
                    throw new ApplicationException($"Cable span equipment with id: {_spanEquipment.Id} has an inconsistent utility network hop. From node id: {utilityHop.FromNodeId} of hop number: {hopNumber} do not exists in the walk of interest: {_spanEquipment.WalkOfInterestId} of the span equipment!");

                if (!_woiContainsId.Contains(utilityHop.ToNodeId))
                    throw new ApplicationException($"Cable span equipment with id: {_spanEquipment.Id} has an inconsistent utility network hop. To node id: {utilityHop.ToNodeId} of hop number: {hopNumber} do not exists in the walk of interest: {_spanEquipment.WalkOfInterestId} of the span equipment!");

                hopNumber++;
            }


            var walkOfInterest = _routeNetworkInterests[_spanEquipment.WalkOfInterestId];

            Stack<UtilityNetworkHop> utilityHopStack = new Stack<UtilityNetworkHop>(_networkHops.ToArray().Reverse());

            RouteNetworkElementIdList routeNetworkHopIds = new();

            double totalLength = 0;

            for (int woiIndex = 0; woiIndex < walkOfInterest.RouteNetworkElementRefs.Count; woiIndex++)
            {
                Guid routeNetworkElementId = walkOfInterest.RouteNetworkElementRefs[woiIndex];

                if (utilityHopStack.Count > 0 && utilityHopStack.Peek().FromNodeId == routeNetworkElementId)
                {
                    // If route network hop contains any elements it means that the cable has been lying directly in the route network before reaching a utility hop
                    if (routeNetworkHopIds.Count > 0)
                    {
                        // Add mising end node id
                        routeNetworkHopIds.Add(routeNetworkElementId);

                        lineInfos.Add(CreateLineInfoForRouteNetworkHop(new ValidatedRouteNetworkWalk(routeNetworkHopIds), ref totalLength));

                        routeNetworkHopIds.Clear();
                    }

                    var utilityHop = utilityHopStack.Pop();

                    if (utilityHop.ParentAffixes == null || utilityHop.ParentAffixes.Count() == 0)
                        throw new ApplicationException($"Cable span equipment with id: {_spanEquipment.Id} has invalid utility network hop. The hop from node: {utilityHop.FromNodeId} to node: {utilityHop.ToNodeId} has no parent affixes. Should have a least one.");


                    lineInfos.AddRange(CreateLineInfosForConduitHops(utilityHop.ParentAffixes[0].SpanSegmentId, utilityHop.FromNodeId, utilityHop.ToNodeId, ref totalLength));

                    // Search for end node
                    for (int searchIndex = woiIndex + 1; searchIndex < walkOfInterest.RouteNetworkElementRefs.Count; searchIndex++)
                    {
                        Guid searchNetworkElementId = walkOfInterest.RouteNetworkElementRefs[searchIndex];

                        if (searchNetworkElementId == utilityHop.ToNodeId)
                            break;

                        woiIndex++;
                    }
                }
                else
                {
                    routeNetworkHopIds.Add(routeNetworkElementId);
                }
            }

            // If route network hop contains more than two elements in the end means that the cable is lying directly in the route network efter the last utility hop or entirely in the route network
            if (routeNetworkHopIds.Count > 2)
            {
                lineInfos.Add(CreateLineInfoForRouteNetworkHop(new ValidatedRouteNetworkWalk(routeNetworkHopIds), ref totalLength));
            }

            return lineInfos;
        }

        private SpanEquipmentPassageViewLineInfo CreateLineInfoForRouteNetworkHop(ValidatedRouteNetworkWalk routeNetworkWoi, ref double totalLength)
        {
            var spanSegmentGeometries = GetSpanSegmentGeometries(routeNetworkWoi);

            var length = GetLineStringsLength(spanSegmentGeometries);
            totalLength += length;

            SpanEquipmentPassageViewLineInfo lineInfo = new SpanEquipmentPassageViewLineInfo(Guid.Empty)
            {
                FromNodeId = routeNetworkWoi.FromNodeId,
                ToNodeId = routeNetworkWoi.ToNodeId,
                From = GetNodeName(routeNetworkWoi.FromNodeId),
                To = GetNodeName(routeNetworkWoi.ToNodeId),
                OuterConduitInfo = "Ligger i tracé",
                RouteSegmentIds = routeNetworkWoi.SegmentIds.ToArray(),
                RouteSegmentGeometries = spanSegmentGeometries.ToArray(),
                SegmentLength = length,
                CumulativeDistance = totalLength
            };

            return lineInfo;
        }
       
    }
}
