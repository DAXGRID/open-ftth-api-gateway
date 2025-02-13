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
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class PlaceAdditionalStructuresInSpanEquipmentCommandHandler : ICommandHandler<PlaceAdditionalStructuresInSpanEquipment, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public PlaceAdditionalStructuresInSpanEquipmentCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(PlaceAdditionalStructuresInSpanEquipment command)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            var spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;

            // Find span equipment
            if (!utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanEquipmentId, out SpanEquipment spanEquipment))
            {
                if (!utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(command.SpanEquipmentId, out var spanSegmentGraphElement))
                    return Task.FromResult(Result.Fail(new PlaceAdditionalStructuresInSpanEquipmentError(PlaceAdditionalStructuresInSpanEquipmentErrorCodes.INVALID_SPAN_EQUIPMENT_ID_NOT_FOUND, $"Cannot find any span equipment with id: {command.SpanEquipmentId}")));
            }

            // Get specification
            var specification = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications[spanEquipment.SpecificationId];

            // Get interest information for both span equipment and node container, which is needed for the aggregate to validate the command
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new InterestIdList() { spanEquipment.WalkOfInterestId })).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got unexpected error result: {interestQueryResult.Errors.First().Message} trying to query interest information for span equipment while processing the PlaceAdditionalStructuresInSpanEquipment command: " + JsonConvert.SerializeObject(command));

            if (interestQueryResult.Value.Interests == null)
                throw new ApplicationException($"Got null interest result processing the PlaceAdditionalStructuresInSpanEquipment command: " + JsonConvert.SerializeObject(command));

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var spanEquipmentAddStructuresResult = spanEquipmentAR.AddAdditionalStructures(
                cmdContext: commandContext,
                specification: specification,
                structureSpecifications: spanStructureSpecifications,
                structureSpecificationIdsToAdd: command.StructureSpecificationIds
            );

            if (spanEquipmentAddStructuresResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(spanEquipmentAR);
                NotifyExternalServicesAboutChange(command.SpanEquipmentId, interestQueryResult.Value.Interests[spanEquipment.WalkOfInterestId].RouteNetworkElementRefs);
            }

            return Task.FromResult(spanEquipmentAddStructuresResult);
        }

        private async void NotifyExternalServicesAboutChange(Guid spanEquipmentId, RouteNetworkElementIdList routeIdsAffected)
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
                    category: "EquipmentModification.StructuresAdded",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: routeIdsAffected.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
