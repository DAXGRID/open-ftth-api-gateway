using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class PlaceNodeContainerInRouteNetworkCommandHandler : ICommandHandler<PlaceNodeContainerInRouteNetwork, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IExternalEventProducer _externalEventProducer;

        public PlaceNodeContainerInRouteNetworkCommandHandler(IEventStore eventStore, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(PlaceNodeContainerInRouteNetwork command)
        {
            var nodeContainers = _eventStore.Projections.Get<UtilityNetworkProjection>().NodeContainerByEquipmentId;
            var nodeContainerSpecifications = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications;

            var nodeContainerAR = new NodeContainerAR();

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var placeSpanEquipmentResult = nodeContainerAR.PlaceNodeContainerInRouteNetworkNode(
                commandContext,
                nodeContainers,
                nodeContainerSpecifications,
                command.NodeContainerId,
                command.NodeContainerSpecificationId,
                command.NodeOfInterest,
                command.NamingInfo,
                command.LifecycleInfo,
                command.ManufacturerId
            );

            if (placeSpanEquipmentResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(nodeContainerAR);
                NotifyExternalServicesAboutChange(command);
            }

            return Task.FromResult(placeSpanEquipmentResult);
        }

        private async void NotifyExternalServicesAboutChange(PlaceNodeContainerInRouteNetwork command)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("NodeContainer", ChangeTypeEnum.Addition, new Guid[] { command.NodeContainerId })
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
                    affectedRouteNetworkElementIds: command.NodeOfInterest.RouteNetworkElementRefs.ToArray()
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
