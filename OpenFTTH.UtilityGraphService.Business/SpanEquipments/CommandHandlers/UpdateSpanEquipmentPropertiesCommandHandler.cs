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
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class UpdateSpanEquipmentPropertiesCommandHandler : ICommandHandler<UpdateSpanEquipmentProperties, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly IQueryDispatcher _queryDispatcher;

        public UpdateSpanEquipmentPropertiesCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(UpdateSpanEquipmentProperties command)
        {
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            // Because the client is allowed to provide either a span equipment or segment id, we need look it up via the utility network graph
            if (!utilityNetwork.TryGetEquipment<SpanEquipment>(command.SpanEquipmentOrSegmentId, out SpanEquipment spanEquipment))
                return Task.FromResult(Result.Fail(new UpdateEquipmentPropertiesError(UpdateEquipmentPropertiesErrorCodes.SPAN_EQUIPMENT_NOT_FOUND, $"Cannot find any span equipment or segment in the utility graph with id: {command.SpanEquipmentOrSegmentId}")));

            var spanEquipmentAR = _eventStore.Aggregates.Load<SpanEquipmentAR>(spanEquipment.Id);

            // Get interest information for span equipment
            var walk = GetInterestInformation(spanEquipment);

            bool somethingChanged = false;

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Check if naming info has been updated
            if (command.NamingInfo != null && !command.NamingInfo.Equals(spanEquipment.NamingInfo))
            {
                var updateNamingInfoResult = spanEquipmentAR.ChangeNamingInfo(
                    cmdContext: commandContext,
                    command.NamingInfo
                );

                if (updateNamingInfoResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateNamingInfoResult.Errors.First()));

                somethingChanged = true;
            }

            // Check if marking info has been updated
            if (command.MarkingInfo != null && !command.MarkingInfo.Equals(spanEquipment.MarkingInfo))
            {
                var updateMarkingInfoResult = spanEquipmentAR.ChangeMarkingInfo(
                    cmdContext: commandContext,
                    command.MarkingInfo
                );

                if (updateMarkingInfoResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateMarkingInfoResult.Errors.First()));

                somethingChanged = true;
            }

            // Check if address info has been updated
            if (command.AddressInfo != null && !command.AddressInfo.Equals(spanEquipment.AddressInfo))
            {
                var updateAddressInfoResult = spanEquipmentAR.ChangeAddressInfo(
                    cmdContext: commandContext,
                    command.AddressInfo
                );

                if (updateAddressInfoResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateAddressInfoResult.Errors.First()));

                somethingChanged = true;
            }


            // Check if manufacturer as been updated
            if (command.ManufacturerId != null && !command.ManufacturerId.Equals(spanEquipment.ManufacturerId))
            {
                var updateManufacturerInfoResult = spanEquipmentAR.ChangeManufacturer(
                    cmdContext: commandContext,
                    command.ManufacturerId.Value
                );

                if (updateManufacturerInfoResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateManufacturerInfoResult.Errors.First()));

                somethingChanged = true;
            }

            // Check if specification has been updated
            if (command.SpecificationId != null && !command.SpecificationId.Equals(spanEquipment.SpecificationId))
            {
                if (!spanEquipmentSpecifications.ContainsKey(command.SpecificationId.Value))
                {
                    return Task.FromResult(Result.Fail(new UpdateEquipmentPropertiesError(UpdateEquipmentPropertiesErrorCodes.SPAN_SPECIFICATION_NOT_FOUND, $"Cannot find any span equipment specification with id: {command.SpecificationId.Value}")));
                }

                var updateSpecificationResult = spanEquipmentAR.ChangeSpecification(
                    cmdContext: commandContext,
                    spanEquipmentSpecifications[spanEquipment.SpecificationId],
                    spanEquipmentSpecifications[command.SpecificationId.Value]
                );

                if (updateSpecificationResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateSpecificationResult.Errors.First()));

                somethingChanged = true;
            }



            if (somethingChanged)
            {
                _eventStore.Aggregates.Store(spanEquipmentAR);

                NotifyExternalServicesAboutSpanEquipmentChange(spanEquipment.Id, walk);

                return Task.FromResult(Result.Ok());
            }
            else
            {
                return Task.FromResult(Result.Fail(new UpdateEquipmentPropertiesError(
                      UpdateEquipmentPropertiesErrorCodes.NO_CHANGE,
                      $"Will not update span equipment, because no difference found in provided arguments compared to the current values of the span equpment.")
                  ));
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

        private async void NotifyExternalServicesAboutSpanEquipmentChange(Guid spanEquipmentId, ValidatedRouteNetworkWalk walk)
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
                    category: "EquipmentModification.PropertiesUpdated",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: walk.RouteNetworkElementRefs.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
