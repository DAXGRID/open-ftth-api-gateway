using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Projections;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.CommandHandlers
{
    public class UpdateRackPropertiesCommandHandler : ICommandHandler<UpdateRackProperties, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public UpdateRackPropertiesCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(UpdateRackProperties command)
        {
            var rackSpecifications = _eventStore.Projections.Get<RackSpecificationsProjection>().Specifications;

            var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(_queryDispatcher, command.RouteNodeId);

            if (getNodeContainerResult.IsFailed)
                return Task.FromResult(Result.Fail(getNodeContainerResult.Errors.First()));

            var nodeContainer = getNodeContainerResult.Value;

            if (nodeContainer.Racks == null || !nodeContainer.Racks.Any(r => r.Id == command.RackId))
                return Task.FromResult(Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {command.RackId} in node container with id: {nodeContainer.Id}")));

            var rack = nodeContainer.Racks.First(r => r.Id == command.RackId);

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(nodeContainer.Id);

            bool somethingChanged = false;

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Check if specification has been updated
            if (command.SpecificationId != null && !command.SpecificationId.Equals(rack.SpecificationId))
            {
                if (!rackSpecifications.ContainsKey(command.SpecificationId.Value))
                {
                    return Task.FromResult(Result.Fail(new UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes.RACK_SPECIFICATION_NOT_FOUND, $"Cannot find any rack specification with id: {command.SpecificationId.Value}")));
                }

                var updateSpecificationResult = nodeContainerAR.ChangeRackSpecification(
                    cmdContext: commandContext,
                    command.RackId,
                    rackSpecifications[rack.SpecificationId],
                    rackSpecifications[command.SpecificationId.Value]
                );

                if (updateSpecificationResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateSpecificationResult.Errors.First()));

                somethingChanged = true;
            }

            // Check if name has been updated
            if (command.Name != null && !command.Name.Equals(rack.Name))
            {
                var updateSpecificationResult = nodeContainerAR.ChangeRackName(
                    cmdContext: commandContext,
                    command.RackId,
                    command.Name
                );

                if (updateSpecificationResult.IsFailed)
                    return Task.FromResult(Result.Fail(updateSpecificationResult.Errors.First()));

                somethingChanged = true;
            }

            // Check if heigh in units has been updated
            if (command.HeightInUnits != null && !command.HeightInUnits.Equals(rack.HeightInUnits))
            {
                var updateSpecificationResult = nodeContainerAR.ChangeRackHeightInUnits(
                    cmdContext: commandContext,
                    command.RackId,
                    command.HeightInUnits.Value
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
