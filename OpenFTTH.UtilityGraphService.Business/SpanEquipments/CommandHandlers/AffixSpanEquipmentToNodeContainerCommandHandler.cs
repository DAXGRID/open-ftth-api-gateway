using DAX.EventProcessing;
using FluentResults;
using Newtonsoft.Json;
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
    public class AffixSpanEquipmentToNodeContainerCommandHandler : ICommandHandler<AffixSpanEquipmentToNodeContainer, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public AffixSpanEquipmentToNodeContainerCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _queryDispatcher = queryDispatcher;
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(AffixSpanEquipmentToNodeContainer command)
        {
            if (command.SpanEquipmentOrSegmentId == Guid.Empty)
                return Task.FromResult(Result.Fail(new AffixSpanEquipmentToNodeContainerError(AffixSpanEquipmentToNodeContainerErrorCodes.INVALID_SPAN_SEGMENT_ID_CANNOT_BE_EMPTY, $"Span segment id must be specified.")));

            if (command.NodeContainerId == Guid.Empty)
                return Task.FromResult(Result.Fail(new AffixSpanEquipmentToNodeContainerError(AffixSpanEquipmentToNodeContainerErrorCodes.INVALID_NODE_CONTAINER_ID_CANNOT_BE_EMPTY, $"Node container id must be specified.")));

            var _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();


            // Find span equipment
            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanEquipmentOrSegmentId, out SpanEquipment spanEquipment))
            {
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanEquipmentOrSegmentId, out var spanSegmentGraphElement))
                    return Task.FromResult(Result.Fail(new AffixSpanEquipmentToNodeContainerError(AffixSpanEquipmentToNodeContainerErrorCodes.INVALID_SPAN_EQUIPMENT_OR_SEGMENT_ID_NOT_FOUND, $"Cannot find any span equipment or span segment with id: {command.SpanEquipmentOrSegmentId}")));

                spanEquipment = spanSegmentGraphElement.SpanEquipment(_utilityNetwork);
            }

            // Find node container
            if (!_utilityNetwork.TryGetEquipment<NodeContainer>(command.NodeContainerId, out var nodeContainer))
                return Task.FromResult(Result.Fail(new AffixSpanEquipmentToNodeContainerError(AffixSpanEquipmentToNodeContainerErrorCodes.INVALID_SPAN_CONTAINER_ID_NOT_FOUND, $"Cannot find any node container with id: {command.NodeContainerId}")));

            // Get interest information for both span equipment and node container, which is needed for the aggregate to validate the command
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId, nodeContainer.InterestId })).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got unexpected error result: {interestQueryResult.Errors.First().Message} trying to query interest information for node container and/or span equipment while processing the AffixSpanEquipmentToNodeContainer command: " + JsonConvert.SerializeObject(command));

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException("No data were unexpectedly returned trying to query interest information for node container and/or span equipment while processing the AffixSpanEquipmentToNodeContainer command: " + JsonConvert.SerializeObject(command));

            if (!interestQueryResult.Value.Interests.TryGetValue(spanEquipment.WalkOfInterestId, out _))
                throw new ApplicationException($"No interest information were unexpectedly returned querying span equipment with id: {spanEquipment.Id} interest id: {spanEquipment.WalkOfInterestId}");

            if (!interestQueryResult.Value.Interests.TryGetValue(nodeContainer.InterestId, out _))
                throw new ApplicationException($"No interest information were unexpectedly returned querying node container with id: {nodeContainer.Id} interest id: {nodeContainer.InterestId}");

            var nodeContainers = _eventStore.Projections.Get<UtilityNetworkProjection>().NodeContainerByEquipmentId;

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var affixResult = spanEquipmentAR.AffixToNodeContainer(
                cmdContext: commandContext,
                nodeContainers: nodeContainers,
                spanEquipmentInterest: interestQueryResult.Value.Interests[spanEquipment.WalkOfInterestId],
                nodeContainerRouteNodeId: interestQueryResult.Value.Interests[nodeContainer.InterestId].RouteNetworkElementRefs[0],
                nodeContainerId: command.NodeContainerId,
                spanSegmentId: command.SpanEquipmentOrSegmentId,
                nodeContainerIngoingSide: command.NodeContainerIngoingSide
            );

            if (affixResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(spanEquipmentAR);

                NotifyExternalServicesAboutChange(spanEquipment.Id, command.NodeContainerId, new Guid[] { interestQueryResult.Value.Interests[nodeContainer.InterestId].RouteNetworkElementRefs[0] });
            }

            return Task.FromResult(affixResult);
        }

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, Guid routeContainerId, Guid[] affectedRouteNetworkElementIds)
        {
            List<IdChangeSet> idChangeSets = new List<IdChangeSet>
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
    }
}
