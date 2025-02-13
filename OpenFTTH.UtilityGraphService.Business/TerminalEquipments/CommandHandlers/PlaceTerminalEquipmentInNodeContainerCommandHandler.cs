using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class PlaceTerminalEquipmentInNodeContainerCommandHandler : ICommandHandler<PlaceTerminalEquipmentInNodeContainer, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private readonly LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;

        public PlaceTerminalEquipmentInNodeContainerCommandHandler(IEventStore eventStore, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _eventStore = eventStore;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;
        }

        public Task<Result> HandleAsync(PlaceTerminalEquipmentInNodeContainer command)
        {
            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Initial validation
            if (command.NumberOfEquipments < 1)
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_NUMBER_OF_EQUIPMENTS_VALUE, $"Number of equipments command parameter must be greater than zero")));

            if (!_terminalEquipmentSpecifications.Any(s => s.Id == command.TerminalEquipmentSpecificationId))
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_TERMINAL_EQUIPMENT_SPECIFICATION_ID_NOT_FOUND, $"Terminal equipment specification with id: {command.TerminalEquipmentSpecificationId} not found")));

            if (!_utilityNetwork.TryGetEquipment<NodeContainer>(command.NodeContainerId, out var nodeContainer))
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.NODE_CONTAINER_NOT_FOUND, $"Cannot find any node container with id: {command.NodeContainerId}")));

            if (command.SubrackPlacementInfo != null)
            {
                if (nodeContainer.Racks == null || !nodeContainer.Racks.Any(r => r.Id == command.SubrackPlacementInfo.RackId))
                    return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.RACK_NOT_FOUND, $"Cannot find rack with id: {command.SubrackPlacementInfo.RackId} in node container with id: {command.NodeContainerId}")));

                if (!_terminalEquipmentSpecifications[command.TerminalEquipmentSpecificationId].IsRackEquipment)
                    return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_TERMINAL_EQUIPMENT_EXPECTED_RACK_EQUIPMENT, $"Cannot add non-rack equipment to rack with id: {command.SubrackPlacementInfo.RackId} in node container with id: {command.NodeContainerId}")));
            }
            else
            {
                if (_terminalEquipmentSpecifications[command.TerminalEquipmentSpecificationId].IsRackEquipment)
                    return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_TERMINAL_EQUIPMENT_EXPECTED_NON_RACK_EQUIPMENT, $"Cannot add a rack equipment directly to node container with id: {command.NodeContainerId}")));
            }


            // Place all terminal equipments
            List<TerminalEquipmentAR> terminalEquipmentARs = new();

            var placeTerminalEquipmentsResult = PlaceTerminalEquipments(command, _terminalEquipmentSpecifications, _terminalStructureSpecifications, commandContext, out terminalEquipmentARs);

            if (placeTerminalEquipmentsResult.IsFailed)
                return Task.FromResult(placeTerminalEquipmentsResult);


            // Add the terminal equipments to node container
            List<Guid> terminalEquipmentIds = new();

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(command.NodeContainerId);

            foreach (var terminalEquipmentAR in terminalEquipmentARs)
            {
                terminalEquipmentIds.Add(terminalEquipmentAR.Id);

                // Standalone equipment placed directly in node container
                if (command.SubrackPlacementInfo == null)
                {
                    var addTerminalEquipmentResult = nodeContainerAR.AddTerminalEquipmentToNode(commandContext, terminalEquipmentAR.Id);

                    if (addTerminalEquipmentResult.IsFailed)
                        return Task.FromResult(addTerminalEquipmentResult);
                }
            }

            // Terminal equipments placed in rack
            if (command.SubrackPlacementInfo != null)
            {
                var addTerminalEquipmentToRackResult = nodeContainerAR.AddTerminalEquipmentsToRack(
                    commandContext,
                    terminalEquipmentIds.ToArray(),
                    _terminalEquipmentSpecifications[command.TerminalEquipmentSpecificationId],
                    command.SubrackPlacementInfo.RackId,
                    command.SubrackPlacementInfo.StartUnitPosition,
                    command.SubrackPlacementInfo.PlacementMethod
                 );

                if (addTerminalEquipmentToRackResult.IsFailed)
                    return Task.FromResult(addTerminalEquipmentToRackResult);
            }


            // Store the aggregates and tell the world
            foreach (var terminalEquipmentAR in terminalEquipmentARs)
            {
                _eventStore.Aggregates.Store(terminalEquipmentAR);
            }

            _eventStore.Aggregates.Store(nodeContainerAR);

            NotifyExternalServicesAboutChange(nodeContainer.RouteNodeId, terminalEquipmentIds.ToArray());

            return Task.FromResult(Result.Ok());
        }

        private Result PlaceTerminalEquipments(PlaceTerminalEquipmentInNodeContainer command, LookupCollection<API.Model.UtilityNetwork.TerminalEquipmentSpecification> terminalEquipmentSpecifications, LookupCollection<API.Model.UtilityNetwork.TerminalStructureSpecification> terminalStructureSpecifications, CommandContext commandContext, out List<TerminalEquipmentAR> terminalEquipmentARs)
        {
            terminalEquipmentARs = new();

            for (var terminalEquipmentSequenceNumber = command.StartSequenceNumber; terminalEquipmentSequenceNumber < (command.StartSequenceNumber + command.NumberOfEquipments); terminalEquipmentSequenceNumber++)
            {
                var terminalEquipmentAR = new TerminalEquipmentAR();

                var terminalEquipmentId = Guid.NewGuid();

                if (command.NumberOfEquipments == 1)
                    terminalEquipmentId = command.TerminalEquipmentId;

                var placeTerminalEquipmentResult = terminalEquipmentAR.Place(
                    commandContext,
                    terminalEquipmentSpecifications,
                    terminalStructureSpecifications,
                    command.NodeContainerId,
                    terminalEquipmentId,
                    command.TerminalEquipmentSpecificationId,
                    terminalEquipmentSequenceNumber,
                    command.NamingMethod,
                    command.NamingInfo,
                    command.LifecycleInfo,
                    command.AddressInfo,
                    command.ManufacturerId
                );

                if (placeTerminalEquipmentResult.IsFailed)
                    return placeTerminalEquipmentResult;

                terminalEquipmentARs.Add(terminalEquipmentAR);
            }

            return Result.Ok();
        }

        private async void NotifyExternalServicesAboutChange(Guid routeNodeId, Guid[] terminalEquipmentIds)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("TerminalEquipment", ChangeTypeEnum.Addition, terminalEquipmentIds)
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
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
