using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Linq;

namespace OpenFTTH.Tests.Util
{
    public class UtilityNetworkBuilder
    {
        private readonly IEventStore _eventStore;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        public UtilityNetworkBuilder(IEventStore eventStore, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _eventStore = eventStore;
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        public Result ConnectTwoSpanSegments(Guid nodeId, Guid fromSpanEquipmentId, int fromSpanStructureIndex, int fromSpanSegmentIndex, Guid toSpanEquipmentId, int toSpanStructureIndex, int toSpanSegmentIndex)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            utilityNetwork.TryGetEquipment<SpanEquipment>(fromSpanEquipmentId, out var fromSpanEquipment);
            utilityNetwork.TryGetEquipment<SpanEquipment>(toSpanEquipmentId, out var toSpanEquipment);

            var connectCmd = new ConnectSpanSegmentsAtRouteNode(Guid.NewGuid(), new UserContext("test", Guid.Empty),
                routeNodeId: nodeId,
                spanSegmentsToConnect: new Guid[] {
                    fromSpanEquipment.SpanStructures[fromSpanStructureIndex].SpanSegments[fromSpanSegmentIndex].Id,
                    toSpanEquipment.SpanStructures[toSpanStructureIndex].SpanSegments[toSpanSegmentIndex].Id
                }
            );

            var result = _commandDispatcher.HandleAsync<ConnectSpanSegmentsAtRouteNode, Result>(connectCmd).Result;

            if (result.IsFailed)
                throw new ApplicationException(result.Errors.First().Message);

            return result;
        }

        
    }
}
