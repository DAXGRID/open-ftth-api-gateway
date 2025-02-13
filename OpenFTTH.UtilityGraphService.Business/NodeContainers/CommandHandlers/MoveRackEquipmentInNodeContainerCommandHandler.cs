using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class MoveRackEquipmentInNodeContainerCommandHandler : ICommandHandler<MoveRackEquipmentInNodeContainer, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly IExternalEventProducer _externalEventProducer;

        public MoveRackEquipmentInNodeContainerCommandHandler(IEventStore eventStore, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _eventStore = eventStore;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result> HandleAsync(MoveRackEquipmentInNodeContainer command)
        {
            if (!_utilityNetwork.TryGetEquipment<NodeContainer>(command.NodeContainerId, out var nodeContainer))
            {
                return Task.FromResult(Result.Fail(new NodeContainerError(NodeContainerErrorCodes.NODE_CONTAINER_NOT_FOUND, $"Cannot find any node container with id: {command.NodeContainerId}")));
            }

            if (!_utilityNetwork.TryGetEquipment<TerminalEquipment>(command.TerminalEquipmentId, out var terminalEquipment))
            {
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_NOT_FOUND, $"Cannot find any terminal equipment with id: {command.TerminalEquipmentId}")));
            }

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(command.NodeContainerId);

            var terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;

            var terminalEquipmentSpecification = terminalEquipmentSpecifications[terminalEquipment.SpecificationId];

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var placeRackResult = nodeContainerAR.MoveRackEquipment(
                commandContext,
                command.TerminalEquipmentId,
                terminalEquipmentSpecification,
                command.MoveToRackId,
                command.MoveToRackPosition
            );

            if (placeRackResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(nodeContainerAR);
                NotifyExternalServicesAboutChange(nodeContainer.RouteNodeId, nodeContainer.Id);
            }

            return Task.FromResult(placeRackResult);
        }

        private async void NotifyExternalServicesAboutChange(Guid routeNodeId, Guid nodeContainerId)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("NodeContainer", ChangeTypeEnum.Addition, new Guid[] { nodeContainerId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentModification.RackEquipmentMoved",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
