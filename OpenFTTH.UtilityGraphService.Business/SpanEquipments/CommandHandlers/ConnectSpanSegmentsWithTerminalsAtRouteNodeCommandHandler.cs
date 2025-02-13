using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class ConnectSpanSegmentsWithTerminalsAtRouteNodeCommandHandler : ICommandHandler<ConnectSpanSegmentsWithTerminalsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly UtilityNetworkProjection _utilityNetwork;

        public ConnectSpanSegmentsWithTerminalsAtRouteNodeCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result> HandleAsync(ConnectSpanSegmentsWithTerminalsAtRouteNode command)
        {
            if (command.Connects.Length == 0)
                return Task.FromResult(Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.INVALID_CONNECTS_LIST_CANNOT_BE_EMPTY, "The list of segment to terminal connections cannot be empty")));

            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.Connects[0].SpanSegmentId, out var firstSpanSegmentGraphElement))
                return Task.FromResult(Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.Connects[0].SpanSegmentId}")));

            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(command.Connects[0].TerminalId, out var firstTerminalGraphElement))
                return Task.FromResult(Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.TERMINAL_NOT_FOUND, $"Cannot find any terminal in the utility graph with id: {command.Connects[0].TerminalId}")));

            // Validate terminal connections
            var validateTerminalResult = ValidateTerminalConnections(command.Connects);

            if (validateTerminalResult.IsFailed)
                return Task.FromResult(validateTerminalResult);

            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var cmdContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(firstSpanSegmentGraphElement.SpanEquipmentId);

            var connects = BuildConnects(command.Connects);

            var spanEquipmentConnectResult = spanEquipmentAR.ConnectCableSpanSegmentsWithTerminals(
                cmdContext: cmdContext,
                routeNodeId: command.RouteNodeId,
                specification: spanEquipmentSpecifications[firstSpanSegmentGraphElement.SpanEquipment(_utilityNetwork).SpecificationId],
                connects: connects
            );

            if (spanEquipmentConnectResult.IsFailed)
                return Task.FromResult(Result.Fail(spanEquipmentConnectResult.Errors.First()));

            _eventStore.Aggregates.Store(spanEquipmentAR);

            NotifyExternalServicesAboutConnectivityChange(firstSpanSegmentGraphElement.SpanEquipmentId, GetNodeIdsFromTerminalIds(connects.Select(c => c.TerminalId)), "FiberConnectivityModification.Connect");


            return Task.FromResult(Result.Ok());
        }

        private Result ValidateTerminalConnections(ConnectSpanSegmentToTerminalOperation[] connects)
        {
            foreach (var connect in connects)
            {
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(connect.TerminalId, out var terminalRef))
                    return Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.TERMINAL_NOT_FOUND, $"Cannot find any terminal in the utility graph with id: {connect.TerminalId}"));

                if (terminalRef.IsDummyEnd)
                    return Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.TERMINAL_NOT_FOUND, $"Cannot connect to dummy terminal"));

                var version = _utilityNetwork.Graph.LatestCommitedVersion;

                if (terminalRef is UtilityGraphConnectedTerminal connectedTerminal)
                {
                    var terminal = terminalRef.Terminal(_utilityNetwork);

                    var terminalConnectionCount = connectedTerminal.InE(version).Count();

                    if (terminal.Direction == TerminalDirectionEnum.BI && terminalConnectionCount == 2)
                        return Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.TERMINAL_ALREADY_CONNECTED, $"Bi directional terminal with id: {connect.TerminalId} is already connected to {terminalConnectionCount} segment(s)"));

                    if (terminal.Direction != TerminalDirectionEnum.BI && terminalConnectionCount == 1)
                        return Result.Fail(new ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes.TERMINAL_ALREADY_CONNECTED, $"Non-bi-directional terminal with id: {connect.TerminalId} is already connected to {terminalConnectionCount} segment(s)"));
                }
            }

            return Result.Ok();
        }

        private SpanSegmentToSimpleTerminalConnectInfo[] BuildConnects(ConnectSpanSegmentToTerminalOperation[] connectsOps)
        {
            List<SpanSegmentToSimpleTerminalConnectInfo> connects = new();

            foreach (var connectOp in connectsOps)
            {
                connects.Add(new SpanSegmentToSimpleTerminalConnectInfo(connectOp.SpanSegmentId, connectOp.TerminalId));
            }

            return connects.ToArray();
        }

        private Guid[] GetNodeIdsFromTerminalIds(IEnumerable<Guid> terminalIds)
        {
            HashSet<Guid> nodeIds = new();

            foreach (var terminalId in terminalIds)
            {
                if (_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(terminalId, out var terminalRef))
                {
                    nodeIds.Add(terminalRef.RouteNodeId);
                }
            }

            return nodeIds.ToArray();
        }

        private ValidatedRouteNetworkWalk GetInterestInformation(SpanEquipment spanEquipment)
        {
            // Get interest information from existing span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got unexpected error result: {interestQueryResult.Errors.First().Message} trying to query interest information for span equipment: {spanEquipment.Id} walk of interest id: {spanEquipment.WalkOfInterestId}");

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException($"Error querying interest information belonging to span equipment with id: {spanEquipment.Id}. No interest information returned from route network service.");

            if (!interestQueryResult.Value.Interests.TryGetValue(spanEquipment.WalkOfInterestId, out var routeNetworkInterest))
                throw new ApplicationException($"Error querying interest information belonging to span equipment with id: {spanEquipment.Id}. No interest information returned from route network service.");

            return new ValidatedRouteNetworkWalk(routeNetworkInterest.RouteNetworkElementRefs);
        }

        private async void NotifyExternalServicesAboutConnectivityChange(Guid spanEquipmentId, Guid[] routeNodeIds, string category)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, new Guid[] { spanEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: category,
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: routeNodeIds
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        private class SpanEquipmentWithConnectsHolder
        {
            public SpanEquipment SpanEquipment { get; }
            public List<SpanSegmentConnectHolder> Connects { get; set; }

            public SpanEquipmentWithConnectsHolder(SpanEquipment spanEquipment)
            {
                SpanEquipment = spanEquipment;
                Connects = new();
            }
        }

        private class SpanSegmentConnectHolder
        {
            public SpanSegmentToSimpleTerminalConnectInfo ConnectInfo { get; }
            public Guid StructureSpecificationId { get; set; }
            public ushort StructureIndex { get; set; }
            public SpanSegmentConnectHolder(SpanSegmentToSimpleTerminalConnectInfo connectInfo)
            {
                ConnectInfo = connectInfo;
            }
        }
    }
}
