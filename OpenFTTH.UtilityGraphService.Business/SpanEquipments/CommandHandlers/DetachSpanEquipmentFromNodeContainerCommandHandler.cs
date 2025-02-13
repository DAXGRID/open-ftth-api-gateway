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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class DetachSpanEquipmentFromNodeContainerCommandHandler : ICommandHandler<DetachSpanEquipmentFromNodeContainer, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public DetachSpanEquipmentFromNodeContainerCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _queryDispatcher = queryDispatcher;
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(DetachSpanEquipmentFromNodeContainer command)
        {
            if (command.SpanEquipmentOrSegmentId == Guid.Empty)
                return Task.FromResult(Result.Fail(new DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes.INVALID_SPAN_SEGMENT_ID_CANNOT_BE_EMPTY, $"Span segment id must be specified.")));

            if (command.RouteNodeId == Guid.Empty)
                return Task.FromResult(Result.Fail(new DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes.INVALID_NODE_CONTAINER_ID_CANNOT_BE_EMPTY, $"Node container id must be specified.")));

            var _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();


            // Find span equipment
            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanEquipmentOrSegmentId, out SpanEquipment spanEquipment))
            {
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanEquipmentOrSegmentId, out var spanSegmentGraphElement))
                    return Task.FromResult(Result.Fail(new DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes.INVALID_SPAN_EQUIPMENT_OR_SEGMENT_ID_NOT_FOUND, $"Cannot find any span equipment or span segment with id: {command.SpanEquipmentOrSegmentId}")));
            }

            // Cable
            if (spanEquipment.IsCable)
            {
                // Get interest information from existing span equipment
                var existingWalk = GetInterestInformation(spanEquipment);

                var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

                var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

                var detachResult = spanEquipmentAR.DetachFromParentsInNode(
                    cmdContext: commandContext,
                    routeNodeId: command.RouteNodeId,
                    existingWalk: existingWalk
                );

                if (detachResult.IsSuccess)
                {
                    _eventStore.Aggregates.Store(spanEquipmentAR);

                    NotifyExternalServicesAboutChange(spanEquipment.Id, new Guid[] { command.RouteNodeId });
                }

                return Task.FromResult(detachResult);

            }
            // Conduit
            else
            {

                // Find node container id in span equipment
                if (spanEquipment.NodeContainerAffixes == null)
                    return Task.FromResult(Result.Fail(new DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_EQUIPMENT_IS_NOT_AFFIXED_TO_NODE_CONTAINER, $"Span equipment with id {spanEquipment.Id} is not affixed to any node container.")));

                if (!spanEquipment.NodeContainerAffixes.Any(n => n.RouteNodeId == command.RouteNodeId))
                    return Task.FromResult(Result.Fail(new DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_EQUIPMENT_IS_NOT_AFFIXED_TO_NODE_CONTAINER, $"Span equipment with id {spanEquipment.Id} is not affixed in route node with id: {command.RouteNodeId}")));

                var nodeContainerId = spanEquipment.NodeContainerAffixes.First(n => n.RouteNodeId == command.RouteNodeId).NodeContainerId;

                // Find node container
                if (!_utilityNetwork.TryGetEquipment<NodeContainer>(nodeContainerId, out var nodeContainer))
                    return Task.FromResult(Result.Fail(new DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes.INVALID_NODE_CONTAINER_ID_NOT_FOUND, $"Cannot find any node container with id: {command.RouteNodeId}")));

                var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

                var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

                var detachResult = spanEquipmentAR.DetachFromNodeContainer(
                    cmdContext: commandContext,
                    nodeContainer: nodeContainer
                );

                if (detachResult.IsSuccess)
                {
                    _eventStore.Aggregates.Store(spanEquipmentAR);

                    NotifyExternalServicesAboutChange(spanEquipment.Id, nodeContainerId, new Guid[] { nodeContainer.RouteNodeId });
                }

                return Task.FromResult(detachResult);
            }
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

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, Guid routeContainerId, Guid[] affectedRouteNetworkElementIds)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, new Guid[] { spanEquipmentId }),
                new IdChangeSet("NodeContainer", ChangeTypeEnum.Modification, new Guid[] { routeContainerId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentModification",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: affectedRouteNetworkElementIds
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, Guid[] affectedRouteNetworkElementIds)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Modification, new Guid[] { spanEquipmentId }),
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentModification",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: affectedRouteNetworkElementIds
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
