using DAX.EventProcessing;
using FluentResults;
using Newtonsoft.Json;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.Interest;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class RemoveSpanStructureFromSpanEquipmentCommandHandler : ICommandHandler<RemoveSpanStructureFromSpanEquipment, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public RemoveSpanStructureFromSpanEquipmentCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(RemoveSpanStructureFromSpanEquipment command)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            if (!utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanSegmentId, out var spanSegmentGraphElement))
                return Task.FromResult(Result.Fail(new RemoveSpanStructureFromSpanEquipmentError(RemoveSpanStructureFromSpanEquipmentErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.SpanSegmentId}")));

            var spanEquipment = spanSegmentGraphElement.SpanEquipment(utilityNetwork);

            // Get specification
            var specification = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications[spanEquipment.SpecificationId];

            // Get interest information for span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got unexpected error result: {interestQueryResult.Errors.First().Message} trying to query interest information for span equipment while processing the RemoveSpanStructureFromSpanEquipment command: " + JsonConvert.SerializeObject(command));

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException($"Got null interest result processing RemoveSpanStructureFromSpanEquipment command: " + JsonConvert.SerializeObject(command));

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // If outer conduit, that remove entire span equipment
            if (spanSegmentGraphElement.StructureIndex == 0)
            {
                // Check that no childs span equipments are related to any span segments
                foreach (var spanStructure in spanEquipment.SpanStructures)
                {
                    foreach (var spanSegment in spanStructure.SpanSegments)
                    {
                        if (utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegment.Id))
                            return Task.FromResult(Result.Fail(new RemoveSpanStructureFromSpanEquipmentError(RemoveSpanStructureFromSpanEquipmentErrorCodes.SPAN_SEGMENT_CONTAIN_CABLE, $"Span equipment with id: {spanEquipment.Id} cannot be deleted, because the span segment id: {spanSegment.Id} contain a cable.")));
                    }
                }

                var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);
                var removeSpanEquipment = spanEquipmentAR.Remove(commandContext);

                if (removeSpanEquipment.IsSuccess)
                {
                    // Remember to remove the walk of interest as well
                    var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

                    var interestAR = _eventStore.Aggregates.Load<InterestAR>(spanEquipment.WalkOfInterestId);

                    OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(commandContext.CorrelationId, commandContext.CmdId, commandContext.UserContext);

                    var unregisterInterestResult = interestAR.UnregisterInterest(routeNetworkCommandContext, interestProjection, spanEquipment.WalkOfInterestId);

                    if (unregisterInterestResult.IsFailed)
                        throw new ApplicationException($"Failed to unregister interest: {spanEquipment.WalkOfInterestId} of span equipment: {spanEquipment.Id} in RemoveSpanStructureFromSpanEquipmentCommandHandler Error: {unregisterInterestResult.Errors.First().Message}");

                    _eventStore.Aggregates.Store(interestAR);
                    _eventStore.Aggregates.Store(spanEquipmentAR);

                    NotifyExternalServicesAboutSpanEquipmentDeletion(spanEquipment.Id, interestQueryResult.Value.Interests[spanEquipment.WalkOfInterestId].RouteNetworkElementRefs);
                }

                return Task.FromResult(removeSpanEquipment);
            }
            else
            {
                var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

                var removeSpanStructure = spanEquipmentAR.RemoveSpanStructure(commandContext, specification, spanSegmentGraphElement.StructureIndex);

                // Check that no childs span structure are related to any span segments in the structure
                foreach (var spanSegment in spanEquipment.SpanStructures[spanSegmentGraphElement.StructureIndex].SpanSegments)
                {
                    if (utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegment.Id))
                        return Task.FromResult(Result.Fail(new RemoveSpanStructureFromSpanEquipmentError(RemoveSpanStructureFromSpanEquipmentErrorCodes.SPAN_SEGMENT_CONTAIN_CABLE, $"Span structure at index: {spanSegmentGraphElement.StructureIndex} in span equipment with id: {spanEquipment.Id} cannot be deleted, because the span segment id: {spanSegment.Id} contain a cable.")));
                }

                if (removeSpanStructure.IsSuccess)
                {
                    _eventStore.Aggregates.Store(spanEquipmentAR);
                    NotifyExternalServicesAboutSpanEquipmentChange(spanEquipment.Id, interestQueryResult.Value.Interests[spanEquipment.WalkOfInterestId].RouteNetworkElementRefs);
                }

                return Task.FromResult(removeSpanStructure);
            }
        }

        private async void NotifyExternalServicesAboutSpanEquipmentChange(Guid spanEquipmentId, RouteNetworkElementIdList routeIdsAffected)
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
                    category: "EquipmentModification.StructuresRemoved",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: routeIdsAffected.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }

        private async void NotifyExternalServicesAboutSpanEquipmentDeletion(Guid spanEquipmentId, RouteNetworkElementIdList routeIdsAffected)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Deletion, new Guid[] { spanEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentDeletion",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: routeIdsAffected.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
