using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class PlaceSpanEquipmentInRouteNetworkCommandHandler : ICommandHandler<PlaceSpanEquipmentInRouteNetwork, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IExternalEventProducer _externalEventProducer;

        public PlaceSpanEquipmentInRouteNetworkCommandHandler(IEventStore eventStore, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(PlaceSpanEquipmentInRouteNetwork command)
        {
            var spanEquipments = _eventStore.Projections.Get<UtilityNetworkProjection>().SpanEquipmentsByEquipmentId;
            var spanEquipmentSpecifications = _eventStore.Projections.Get<SpanEquipmentSpecificationsProjection>().Specifications;

            var spanEquipmentAR = new SpanEquipmentAR();

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var placeSpanEquipmentResult = spanEquipmentAR.PlaceSpanEquipmentInRouteNetwork(
                cmdContext: commandContext,
                spanEquipments,
                spanEquipmentSpecifications,
                command.SpanEquipmentId,
                command.SpanEquipmentSpecificationId,
                command.Interest,
                command.ManufacturerId,
                command.NamingInfo,
                command.LifecycleInfo,
                command.MarkingInfo,
                command.AddressInfo
            );

            if (placeSpanEquipmentResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(spanEquipmentAR);
                NotifyExternalServicesAboutChange(command);
            }

            return Task.FromResult(placeSpanEquipmentResult);
        }

        private async void NotifyExternalServicesAboutChange(PlaceSpanEquipmentInRouteNetwork spanEquipmentCommand)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("SpanEquipment", ChangeTypeEnum.Addition, new Guid[] { spanEquipmentCommand.SpanEquipmentId })
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
                    affectedRouteNetworkElementIds: spanEquipmentCommand.Interest.RouteNetworkElementRefs.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
