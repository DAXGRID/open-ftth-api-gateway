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
    public class ConduitPassageViewBuilder : PassageViewBuilder
    {
        Dictionary<Guid, UtilityNetworkHop> _networkHopByFromNodeId = new();

        HashSet<Guid> _woiContainsId = new();

        public ConduitPassageViewBuilder(IEventStore eventStore, UtilityNetworkProjection utilityNetwork, IQueryDispatcher queryDispatcher, Guid routeNetworkElementId, SpanEquipment spanEquipment, Guid segmentIdToTrace) : base(eventStore, utilityNetwork, queryDispatcher, routeNetworkElementId, spanEquipment, segmentIdToTrace)
        {
            if (_spanEquipment.IsCable)
                throw new ApplicationException("ConduitPassageViewBuilder can only be used on non-cable span equipments");
        }

        public SpanEquipmentPassageViewEquipmentInfo GetConduitPassageView()
        {
            return new SpanEquipmentPassageViewEquipmentInfo(
                id: _spanEquipment.Id,
                category: _spanEquipmentSpecification.Category,
                name: GetName(),
                specName: GetSpecName(),
                lines: BuildConduitLineInfos().ToArray()
            );
        }

        private string GetSpecName()
        {
            var outerConduitSpecName = _spanEquipmentSpecification.Name;

            return outerConduitSpecName;
        }

        public List<SpanEquipmentPassageViewLineInfo> BuildConduitLineInfos()
        {
            List<SpanEquipmentPassageViewLineInfo> lineInfos = new();

            double totalLength = 0;

            lineInfos.AddRange(CreateLineInfosForConduitHops(_segmentIdToTrace.Value, Guid.Empty, Guid.Empty, ref totalLength));

            ReverseIfNeeded(lineInfos);

            return lineInfos;
        }

        private void ReverseIfNeeded(List<SpanEquipmentPassageViewLineInfo> lineInfos)
        {
            var fromNodeId = lineInfos.First().FromNodeId;
            var toNodeId = lineInfos.Last().ToNodeId;

            var highRankNodeId = TryFindAEndNodeId(fromNodeId, toNodeId);

            if (highRankNodeId != fromNodeId)
                ReverseLineInfos(lineInfos);
        }
    }
}
