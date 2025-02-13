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
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class MoveSpanEquipmentCommandHandler : ICommandHandler<MoveSpanEquipment, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public MoveSpanEquipmentCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            _routeNetworkRepository = routeNodeRepository;
        }

        public Task<Result> HandleAsync(MoveSpanEquipment command)
        {
            // Because the client is allowed to provide either a span equipment or segment id, we need look it up via the utility network graph
            if (!_utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanEquipmentOrSegmentId, out SpanEquipment spanEquipment))
                return Task.FromResult(Result.Fail(new MoveSpanEquipmentError(MoveSpanEquipmentErrorCodes.SPAN_EQUIPMENT_NOT_FOUND, $"Cannot find any span equipment or segment in the utility graph with id: {command.SpanEquipmentOrSegmentId}")));

            // Get interest information from existing span equipment
            var existingWalk = GetInterestInformation(spanEquipment);

            // Validate the new walk
            var walkValidator = new WalkValidator(_routeNetworkRepository);

            var newWalkValidationResult = walkValidator.ValidateWalk(command.NewWalkIds);

            // If the new walk fails to validate, return the error to the client
            if (newWalkValidationResult.IsFailed)
                return Task.FromResult(Result.Fail(newWalkValidationResult.Errors.First()));

            var newWalk = new ValidatedRouteNetworkWalk(newWalkValidationResult.Value);

            // If the walk has not changed return error as well
            if (existingWalk.Equals(newWalk))
                return Task.FromResult(Result.Fail(new MoveSpanEquipmentError(MoveSpanEquipmentErrorCodes.NEW_WALK_EQUALS_EXISTING_WALK, $"The new walk specified is not different from the existing walk of the span equipment.")));

            // Reverse new walk if one of its ends are opposite of existing walk ends
            if (newWalk.FromNodeId == existingWalk.ToNodeId || newWalk.ToNodeId == existingWalk.FromNodeId)
                newWalk = newWalk.Reverse();

            // Try to do the move of the span equipment
            List<AggregateBase> parentARsToStore = new();

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var moveSpanEquipmentResult = spanEquipmentAR.Move(commandContext, newWalk, existingWalk);

            if (moveSpanEquipmentResult.IsFailed)
                return Task.FromResult(Result.Fail(moveSpanEquipmentResult.Errors.First()));

            parentARsToStore.Add(spanEquipmentAR);

            // If span equipment contains cable, move these as well
            Dictionary<Guid, ValidatedRouteNetworkWalk> childWalkOfInterestsToUpdate = new();

            List<AggregateBase> childARsToStore = new();

            if (HasAnyChildSpanEquipments(spanEquipment))
            {
                var children = GetChildSpanEquipments(spanEquipment);

                foreach (var child in children)
                {
                    var childSpanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(child.Id);

                    var existingChildWalk = GetInterestInformation(child);

                    var hopQueryHelper = new UtilityNetworkHopQueryHelper(_queryDispatcher, _utilityNetwork);

                    var childMoveResult = childSpanEquipmentAR.MoveWithParent(commandContext, existingChildWalk, spanEquipment, newWalk, existingWalk, hopQueryHelper);

                    if (childMoveResult.IsFailed)
                        return Task.FromResult(Result.Fail(childMoveResult.Errors.First()));

                    var newChildWalk = childMoveResult.Value;

                    if (!existingChildWalk.Equals(newChildWalk))
                    {
                        // Validate the new child walk
                        var newChildWalkValidationResult = walkValidator.ValidateWalk(childMoveResult.Value.RouteNetworkElementRefs);

                        // If the new walk fails to validate, return the error to the client
                        if (newChildWalkValidationResult.IsFailed)
                            return Task.FromResult(Result.Fail(newChildWalkValidationResult.Errors.First()));

                        childWalkOfInterestsToUpdate.Add(child.WalkOfInterestId, newChildWalk);
                    }

                    childARsToStore.Add(childSpanEquipmentAR);
                }
            }

            // If we got to here, then the span equipment move was validated fine, so we can update the walk of interest
            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            // Move the parent
            var spanEquipmentInterestAR = _eventStore.Aggregates.Load<InterestAR>(spanEquipment.WalkOfInterestId);

            OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(commandContext.CorrelationId, commandContext.CmdId, commandContext.UserContext);

            var newSegmentIds = new RouteNetworkElementIdList();

            newSegmentIds.AddRange(newWalk.SegmentIds);

            var walkOfInterest = new RouteNetworkInterest(spanEquipment.WalkOfInterestId, RouteNetworkInterestKindEnum.WalkOfInterest, newSegmentIds);

            var updateInterestResult = spanEquipmentInterestAR.UpdateRouteNetworkElements(routeNetworkCommandContext, walkOfInterest, interestProjection, new WalkValidator(_routeNetworkRepository));

            if (updateInterestResult.IsFailed)
                throw new ApplicationException($"Failed to update interest: {spanEquipment.WalkOfInterestId} of span equipment: {spanEquipment.Id} in RemoveSpanStructureFromSpanEquipmentCommandHandler Error: {updateInterestResult.Errors.First().Message}");

            parentARsToStore.Add(spanEquipmentInterestAR);

            // Update eventually child walk of interests
            foreach (var childWalkOfInterestToUpdate in childWalkOfInterestsToUpdate)
            {
                var childEquipmentInterestAR = _eventStore.Aggregates.Load<InterestAR>(childWalkOfInterestToUpdate.Key);

                var childWalkOfInterest = new RouteNetworkInterest(spanEquipment.WalkOfInterestId, RouteNetworkInterestKindEnum.WalkOfInterest, childWalkOfInterestToUpdate.Value.RouteNetworkElementRefs);

                var childUpdateInterestResult = childEquipmentInterestAR.UpdateRouteNetworkElements(routeNetworkCommandContext, childWalkOfInterest, interestProjection, walkValidator);

                if (childUpdateInterestResult.IsFailed)
                    throw new ApplicationException($"Failed to update interest: {spanEquipment.WalkOfInterestId} of child span equipment: {spanEquipment.Id} in RemoveSpanStructureFromSpanEquipmentCommandHandler Error: {childUpdateInterestResult.Errors.First().Message}");

                childARsToStore.Add(childEquipmentInterestAR);
            }

            // Store child aggregates
            foreach (var childAR in childARsToStore)
            {
                if (childAR.GetUncommittedEvents().Count() > 0)
                    _eventStore.Aggregates.Store(childAR);
            }

            // Store parent aggregate
            foreach (var parentAR in parentARsToStore)
            {
                if (parentAR.GetUncommittedEvents().Count() > 0)
                    _eventStore.Aggregates.Store(parentAR);
            }
         
                   
            NotifyExternalServicesAboutSpanEquipmentChange(spanEquipment.Id, existingWalk, newWalk);

            return Task.FromResult(moveSpanEquipmentResult);
        }

        private bool HasAnyChildSpanEquipments(SpanEquipment spanEquipment)
        {
            foreach (var spanStructure in spanEquipment.SpanStructures)
            {
                foreach (var spanSegment in spanStructure.SpanSegments)
                {
                    if (_utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegment.Id))
                        return true;
                }
            }

            return false;
        }

        private List<SpanEquipment> GetChildSpanEquipments(SpanEquipment spanEquipment)
        {
            List<SpanEquipment> result = new();

            foreach (var spanStructure in spanEquipment.SpanStructures)
            {
                foreach (var spanSegment in spanStructure.SpanSegments)
                {
                    if (_utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegment.Id))
                    {
                        var childIds = _utilityNetwork.RelatedCablesByConduitSegmentId[spanSegment.Id];

                        foreach (var childId in childIds)
                        {
                            if (_utilityNetwork.TryGetEquipment<SpanEquipment>(childId, out var child))
                            {
                                result.Add(child);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private bool IsChildOfSpanEquipments(SpanEquipment spanEquipment)
        {
            if (spanEquipment.UtilityNetworkHops != null && spanEquipment.UtilityNetworkHops.Count() > 0)
                return true;
            else
                return false;
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

        private async void NotifyExternalServicesAboutSpanEquipmentChange(Guid spanEquipmentId, ValidatedRouteNetworkWalk existingWalk, ValidatedRouteNetworkWalk newWalk)
        {
            var routeIdsAffected = new RouteNetworkElementIdList();

            foreach (var id in existingWalk.RouteNetworkElementRefs)
                routeIdsAffected.Add(id);

            foreach (var id in newWalk.RouteNetworkElementRefs)
            {
                if (!routeIdsAffected.Contains(id))
                    routeIdsAffected.Add(id);
            }

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
                    category: "EquipmentModification.Moved",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: routeIdsAffected.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
