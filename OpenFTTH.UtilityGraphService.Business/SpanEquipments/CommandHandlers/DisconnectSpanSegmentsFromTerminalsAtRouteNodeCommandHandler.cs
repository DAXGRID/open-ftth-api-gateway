using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class DisconnectSpanSegmentsFromTerminalsAtRouteNodeCommandHandler : ICommandHandler<DisconnectSpanSegmentsFromTerminalsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly UtilityNetworkProjection _utilityNetwork;

        public DisconnectSpanSegmentsFromTerminalsAtRouteNodeCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result> HandleAsync(DisconnectSpanSegmentsFromTerminalsAtRouteNode command)
        {
            if (command.Disconnects.Length == 0)
                return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_DISCONNECT_LIST_CANNOT_BE_EMPTY, "The list of span and terminals to disconnect cannot be empty")));

            // Check if terminal to terminal connection
            if (command.Disconnects.Length == 1 && IsTerminalToTerminalLink(command.Disconnects.First().TerminalId, command.Disconnects.First().SpanSegmentId))
            {
                var terminal1Id = command.Disconnects.First().TerminalId;

                var terminalToTerminalLinkId = command.Disconnects.First().SpanSegmentId;


                // Lookup the terminal
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminal1Id, out var terminal1Ref))
                    return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.CONNECTED_TERMINAL_NOT_FOUND, $"Cannot find any terminal in the utility graph with id: {command.Disconnects[0].TerminalId}")));

                var terminalEquipment1 = terminal1Ref.TerminalEquipment(_utilityNetwork);

                // Get other end terminal
                var terminal2Ref = FindOtherEndTerminal(terminal1Id, terminalToTerminalLinkId);

                var terminalEquipment2 = terminal2Ref.TerminalEquipment(_utilityNetwork);

                // Get node container
                var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(_queryDispatcher, command.RouteNodeId);

                if (getNodeContainerResult.IsFailed)
                    return Task.FromResult(Result.Fail(getNodeContainerResult.Errors.First()));

                var nodeContainer = getNodeContainerResult.Value;

                var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(nodeContainer.Id);


                var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

                var disconnectResult = nodeContainerAR.DisconnectTerminals(
                       commandContext,
                       _utilityNetwork.Graph,
                        terminalEquipment1,
                        terminal1Ref.TerminalId,
                        terminalEquipment2,
                        terminal2Ref.TerminalId
                );


                if (disconnectResult.IsSuccess)
                {
                    _eventStore.Aggregates.Store(nodeContainerAR);
                    NotifyExternalServicesAboutChange(nodeContainer.RouteNodeId, new Guid[] { });
                }

                return Task.FromResult(disconnectResult);
            }
            else
            {
                // Lookup the span equipment
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.Disconnects[0].SpanSegmentId, out var spanSegmentRef))
                    return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.Disconnects[0].SpanSegmentId}")));

                var spanEquipment = spanSegmentRef.SpanEquipment(_utilityNetwork);
                var spanSegment = spanSegmentRef.SpanSegment(_utilityNetwork);

                // Check that span segment is connected to route node
                if (spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex] != command.RouteNodeId
                    && spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex] != command.RouteNodeId)
                {
                    return Task.FromResult(Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_IS_NOT_RELATED_TO_ROUTE_NODE, $"The span segment with id: {spanSegment.Id} is not related to route node: {command.RouteNodeId} in any way. Please check command arguments.")));
                }


                // Disconnect the segments
                var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

                var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

                var disconnectResult = spanEquipmentAR.DisconnectSegmentsFromTerminals(
                    cmdContext: commandContext,
                    command.Disconnects
                );

                if (disconnectResult.IsSuccess)
                {
                    _eventStore.Aggregates.Store(spanEquipmentAR);

                    NotifyExternalServicesAboutChange(command.RouteNodeId, new Guid[] { spanEquipment.Id });
                }

                return Task.FromResult(disconnectResult);
            }
        }

        private UtilityGraphConnectedTerminal FindOtherEndTerminal(Guid terminalId, Guid terminalToTerminalLinkId)
        {
            var terminalToTerminalConnectivityLink = GetTerminalToTerminalConnectivityLink(terminalId, terminalToTerminalLinkId);

            if (terminalToTerminalConnectivityLink == null)
                throw new ApplicationException($"Error looking up UtilityGraphTerminalToTerminalConnectivityLink by id: {terminalToTerminalLinkId}");

            var version = _utilityNetwork.Graph.LatestCommitedVersion;

            var otherEndTerminal = terminalToTerminalConnectivityLink.NeighborElements(version).FirstOrDefault(n => n.Id != terminalId) as UtilityGraphConnectedTerminal;

            if (otherEndTerminal == null)
                throw new ApplicationException($"Error finding terminal opposite to terminal: {terminalId} of UtilityGraphTerminalToTerminalConnectivityLink: {terminalToTerminalLinkId}");

            return otherEndTerminal;
        }

        private bool IsTerminalToTerminalLink(Guid terminalId, Guid segmentId)
        {
            var link = GetTerminalToTerminalConnectivityLink(terminalId, segmentId);

            if (link != null)
                return true;
            else
                return false;
        }

        private UtilityGraphTerminalToTerminalConnectivityLink? GetTerminalToTerminalConnectivityLink(Guid terminalId, Guid terminalToTerminalLinkId)
        {
            if (!_utilityNetwork.Graph.TryGetGraphElement<UtilityGraphConnectedTerminal>(terminalId, out var terminal))
                throw new ApplicationException($"Error looking up connected terminal by id: {terminalId}");

            var version = _utilityNetwork.Graph.LatestCommitedVersion;

            var terminalToTerminalLink = terminal.NeighborElements(version).FirstOrDefault(n => n.Id == terminalToTerminalLinkId) as UtilityGraphTerminalToTerminalConnectivityLink;

            return terminalToTerminalLink;
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
                    category: "FiberConnectivityModification.Disconnect",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}

