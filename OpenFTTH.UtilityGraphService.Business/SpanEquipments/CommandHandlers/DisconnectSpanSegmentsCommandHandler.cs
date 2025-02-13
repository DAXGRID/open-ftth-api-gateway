using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class DisconnectSpanSegmentsCommandHandler : ICommandHandler<DisconnectSpanSegmentsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public DisconnectSpanSegmentsCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(DisconnectSpanSegmentsAtRouteNode command)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            if (command.SpanSegmentsToDisconnect.Length != 2)
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_LIST_MUST_CONTAIN_TWO_SPAN_SEGMENT_IDS, "The list of span segments to connect must contain two span segment ids.")));

            // Because the client do not provide the span equipment ids, but span segment ids only,
            // we need lookup the span equipments via the the utility network graph
            SpanEquipment[] spanEquipmentsToDisconnect = new SpanEquipment[2];

            // Lookup the first span equipment
            if (!utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanSegmentsToDisconnect[0], out var firstSpanSegmentGraphElement))
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.SpanSegmentsToDisconnect[0]}")));

            var firstSpanEquipment = firstSpanSegmentGraphElement.SpanEquipment(utilityNetwork);
            var firstSpanSegment = firstSpanSegmentGraphElement.SpanSegment(utilityNetwork);

            // Lookup the second span equipment
            if (!utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanSegmentsToDisconnect[1], out var secondSpanSegmentGraphElement))
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.SpanSegmentsToDisconnect[1]}")));

            var secondSpanEquipment = secondSpanSegmentGraphElement.SpanEquipment(utilityNetwork);
            var secondSpanSegment = secondSpanSegmentGraphElement.SpanSegment(utilityNetwork);

            // Check that the two span segments specified is not the same
            if (firstSpanSegment.Id == secondSpanSegment.Id)
            {
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.CANNOT_DISCONNECT_SPAN_SEGMENT_TO_ITSELF, $"Cannot connect the same span segment to itself")));
            }

            // Check that first span segment is connected to route node
            if (firstSpanEquipment.NodesOfInterestIds[firstSpanSegment.FromNodeOfInterestIndex] != command.RouteNodeId
                && firstSpanEquipment.NodesOfInterestIds[firstSpanSegment.ToNodeOfInterestIndex] != command.RouteNodeId)
            {
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_IS_NOT_RELATED_TO_ROUTE_NODE, $"The span segment with id: {firstSpanSegment.Id} is not related to route node: {command.RouteNodeId} in any way. Please check command arguments.")));
            }

            // Check that second span segment is connected to route node
            if (secondSpanEquipment.NodesOfInterestIds[secondSpanSegment.FromNodeOfInterestIndex] != command.RouteNodeId
                && secondSpanEquipment.NodesOfInterestIds[secondSpanSegment.ToNodeOfInterestIndex] != command.RouteNodeId)
            {
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_IS_NOT_RELATED_TO_ROUTE_NODE, $"The span segment with id: {secondSpanSegment.Id} is not related to route node: {command.RouteNodeId} in any way. Please check command arguments.")));
            }

            // Check that the two segments are connected
            HashSet<Guid> firstSegmentTerminalIds = new HashSet<Guid>();
            if (firstSpanSegment.FromTerminalId != Guid.Empty)
                firstSegmentTerminalIds.Add(firstSpanSegment.FromTerminalId);
            if (firstSpanSegment.ToTerminalId != Guid.Empty)
                firstSegmentTerminalIds.Add(firstSpanSegment.ToTerminalId);

            Guid sharedTerminalId = Guid.Empty;

            if (firstSegmentTerminalIds.Contains(secondSpanSegment.FromTerminalId))
                sharedTerminalId = secondSpanSegment.FromTerminalId;
            else if (firstSegmentTerminalIds.Contains(secondSpanSegment.ToTerminalId))
                sharedTerminalId = secondSpanSegment.ToTerminalId;

            if (sharedTerminalId == Guid.Empty)
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENTS_ARE_NOT_CONNECTED, $"The span segment with id: {firstSpanSegment.Id} and The span segment with id: {secondSpanSegment.Id} is not connected in route node: {command.RouteNodeId}. Please check command arguments.")));

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            if (firstSpanEquipment.Id != secondSpanEquipment.Id)
            {

                // Disconnect the first span equipment from the terminal
                var firstSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(firstSpanEquipment.Id);

                var firstSpanEquipmentConnectResult = firstSpanEquipmentAR.DisconnectSegmentFromTerminal(
                    cmdContext: commandContext,
                    spanSegmentId: firstSpanSegment.Id,
                    terminalId: sharedTerminalId
                );

                if (!firstSpanEquipmentConnectResult.IsSuccess)
                    return Task.FromResult(firstSpanEquipmentConnectResult);

                // Disconnect the second span equipment from the terminal
                var secondSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(secondSpanEquipment.Id);

                var secondSpanEquipmentConnectResult = secondSpanEquipmentAR.DisconnectSegmentFromTerminal(
                    cmdContext: commandContext,
                    spanSegmentId: secondSpanSegment.Id,
                    terminalId: sharedTerminalId
                );

                if (!secondSpanEquipmentConnectResult.IsSuccess)
                    return Task.FromResult(secondSpanEquipmentConnectResult);

                _eventStore.Aggregates.Store(firstSpanEquipmentAR);
                _eventStore.Aggregates.Store(secondSpanEquipmentAR);

                NotifyExternalServicesAboutChange(command.RouteNodeId, new Guid[] { firstSpanEquipment.Id, secondSpanEquipment.Id });
            }
            else
            {
                // Disconnect the first span equipment from the terminal
                var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(firstSpanEquipment.Id);

                var firstConnectResult = spanEquipmentAR.DisconnectSegmentFromTerminal(
                    cmdContext: commandContext,
                    spanSegmentId: firstSpanSegment.Id,
                    terminalId: sharedTerminalId
                );

                if (!firstConnectResult.IsSuccess)
                    return Task.FromResult(firstConnectResult);

                var secondConnectResult = spanEquipmentAR.DisconnectSegmentFromTerminal(
                    cmdContext: commandContext,
                    spanSegmentId: secondSpanSegment.Id,
                    terminalId: sharedTerminalId
                );

                if (!secondConnectResult.IsSuccess)
                    return Task.FromResult(secondConnectResult);

                _eventStore.Aggregates.Store(spanEquipmentAR);

                NotifyExternalServicesAboutChange(command.RouteNodeId, new Guid[] { spanEquipmentAR.Id });

            }

            return Task.FromResult(Result.Ok());
        }

        private async void NotifyExternalServicesAboutChange(Guid routeNodeId, Guid[] spanEquipmentIds)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, spanEquipmentIds)
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentConnectivityModification.Disconnect",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
