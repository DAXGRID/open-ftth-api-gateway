using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.CommandHandlers
{
    public class UpdateNodeContainerPropertiesCommandHandler : ICommandHandler<UpdateNodeContainerProperties, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IExternalEventProducer _externalEventProducer;

        public UpdateNodeContainerPropertiesCommandHandler(IEventStore eventStore, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(UpdateNodeContainerProperties command)
        {
            var nodeContainerSpecifications = _eventStore.Projections.Get<NodeContainerSpecificationsProjection>().Specifications;

            var nodeContainers = _eventStore.Projections.Get<UtilityNetworkProjection>().NodeContainerByEquipmentId;

            if (!nodeContainers.TryGetValue(command.NodeContainerId, out var nodeContainer))
            {
                return Task.FromResult(Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.NODE_CONTAINER_NOT_FOUND, $"Cannot find any node container with id: {command.NodeContainerId}")));
            }

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(command.NodeContainerId);

            bool somethingChanged = false;

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Check if manufacturer as been updated
            if (command.ManufacturerId != null && !command.ManufacturerId.Equals(nodeContainer.ManufacturerId))
            {
                var updateManufacturerInfoResult = nodeContainerAR.ChangeManufacturer(
                    cmdContext: commandContext,
                    command.ManufacturerId.Value
                );

                if (updateManufacturerInfoResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateManufacturerInfoResult.Errors.First()));

                somethingChanged = true;
            }

            // Check if specification has been updated
            if (command.SpecificationId != null && !command.SpecificationId.Equals(nodeContainer.SpecificationId))
            {
                if (!nodeContainerSpecifications.ContainsKey(command.SpecificationId.Value))
                {
                    return Task.FromResult(Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.NODE_CONTAINER_SPECIFICATION_NOT_FOUND, $"Cannot find any node container specification with id: {command.SpecificationId.Value}")));
                }

                var updateSpecificationResult = nodeContainerAR.ChangeSpecification(
                    cmdContext: commandContext,
                    nodeContainerSpecifications[nodeContainer.SpecificationId],
                    nodeContainerSpecifications[command.SpecificationId.Value]
                );

                if (updateSpecificationResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateSpecificationResult.Errors.First()));

                somethingChanged = true;
            }



            if (somethingChanged)
            {
                _eventStore.Aggregates.Store(nodeContainerAR);

                NotifyExternalServicesAboutNodeContainerChange(nodeContainer.Id, nodeContainer.RouteNodeId);

                return Task.FromResult(Result.Ok());
            }
            else
            {
                return Task.FromResult(Result.Fail(new UpdateNodeContainerPropertiesError(
                      UpdateNodeContainerPropertiesErrorCodes.NO_CHANGE,
                      $"Will not update node container, because no difference found in provided arguments compared to the current values of the node container.")
                  ));
            }
        }

        private async void NotifyExternalServicesAboutNodeContainerChange(Guid nodeContainerId, Guid interestId)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("NodeContainer", ChangeTypeEnum.Modification, new Guid[] { nodeContainerId })
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
                    affectedRouteNetworkElementIds: new Guid[] { interestId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
