using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class CutSpanSegmentsCommandHandler : ICommandHandler<CutSpanSegmentsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public CutSpanSegmentsCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(CutSpanSegmentsAtRouteNode command)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            if (command.SpanSegmentsToCut.Length == 0)
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_LIST_CANNOT_BE_EMPTY, "A list of span segments to cut must be provided.")));

            // Because the client is not required to provide the span equipment id (that we need to lookup the 
            // aggregate root), we look it up via the utility network graph.
            if (!utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanSegmentsToCut[0], out var spanSegmentGraphElement))
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment in the utility graph with id: {command.SpanSegmentsToCut[0]}")));


            foreach (var spanSegmentToCut in command.SpanSegmentsToCut)
            {
                if (utilityNetwork.CheckIfConduitSegmentContainsCables(spanSegmentToCut))
                    return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_CONTAIN_CABLE, $"The span segment id: {spanSegmentToCut} contain a cable. Cannot be cut.")));
            }



            var spanEquipment = spanSegmentGraphElement.SpanEquipment(utilityNetwork);
            var firstSpanSegment = spanSegmentGraphElement.SpanSegment(utilityNetwork);

            // Get walk of interest of the span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })
            ).Result;

            if (interestQueryResult.IsFailed)
                return Task.FromResult(Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.FAILED_TO_GET_SPAN_EQUIPMENT_WALK_OF_INTEREST_INFORMATION, $"Got error trying to query interest information belonging to span equipment with id: {spanEquipment.Id} Error Message: {interestQueryResult.Errors.First().Message}")));

            if (interestQueryResult.Value is null || interestQueryResult.Value.Interests is null)
                throw new ApplicationException($"Got nothing back trying to query interest information belonging to span equipment with id: {spanEquipment.Id} Null was returned.");

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var cuteSpanEquipmentsResult = spanEquipmentAR.CutSpanSegments(
                cmdContext: commandContext,
                spanEquipmentWalkOfInterest: interestQueryResult.Value.Interests.First(),
                routeNodeId: command.RouteNodeId,
                spanSegmentsToCut: command.SpanSegmentsToCut
            );

            if (cuteSpanEquipmentsResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(spanEquipmentAR);
                NotifyExternalServicesAboutChange(spanEquipment.Id, command.RouteNodeId);
            }

            return Task.FromResult(cuteSpanEquipmentsResult);
        }

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, Guid routeNodeId)
        {
            List<IdChangeSet> idChangeSets = new List<IdChangeSet>
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
                    category: "EquipmentConnectivityModification.Cut",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
