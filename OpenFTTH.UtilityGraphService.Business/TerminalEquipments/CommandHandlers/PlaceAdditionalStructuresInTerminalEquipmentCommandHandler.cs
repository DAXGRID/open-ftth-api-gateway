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
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class PlaceAdditionalStructuresInTerminalEquipmentCommandHandler : ICommandHandler<PlaceAdditionalStructuresInTerminalEquipment, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly UtilityNetworkProjection _utilityNetwork;
        private readonly IExternalEventProducer _externalEventProducer;
        private readonly LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications;
        private readonly LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications;

        public PlaceAdditionalStructuresInTerminalEquipmentCommandHandler(IEventStore eventStore, IExternalEventProducer externalEventProducer)
        {
            _externalEventProducer = externalEventProducer;
            _eventStore = eventStore;
            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
            _terminalEquipmentSpecifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>().Specifications;
            _terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;
        }

        public Task<Result> HandleAsync(PlaceAdditionalStructuresInTerminalEquipment command)
        {
            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Initial validation
            if (command.NumberOfStructures < 1)
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_NUMBER_OF_EQUIPMENTS_VALUE, $"Number of equipments command parameter must be greater than zero")));

            if (!_terminalStructureSpecifications.TryGetValue(command.StructureSpecificationId, out var terminalStructureSpecification))
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.INVALID_TERMINAL_STRUCTURE_SPECIFICATION_ID_NOT_FOUND, $"Terminal structure specification with id: {command.StructureSpecificationId} not found")));

            // Place terminal equipment structures
            var terminalEquipmentAR = _eventStore.Aggregates.Load<TerminalEquipmentAR>(command.TerminalEquipmentId);

            if (command.InterfaceInfo != null)
            {
                var addStructuresResult = terminalEquipmentAR.AddInterface(commandContext, terminalStructureSpecification, command.InterfaceInfo);

                if (addStructuresResult.IsFailed)
                {
                    return Task.FromResult(addStructuresResult);
                }
            }
            else
            {
                var addStructuresResult = terminalEquipmentAR.AddAdditionalStructures(commandContext, terminalStructureSpecification, command.Position, command.NumberOfStructures);

                if (addStructuresResult.IsFailed)
                {
                    return Task.FromResult(addStructuresResult);
                }
            }

            _eventStore.Aggregates.Store(terminalEquipmentAR);

            NotifyExternalServicesAboutChange(command.RouteNodeId, command.TerminalEquipmentId);

            return Task.FromResult(Result.Ok());
        }

        private async void NotifyExternalServicesAboutChange(Guid routeNodeId, Guid terminalEquipmentId)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("TerminalEquipment", ChangeTypeEnum.Addition, new Guid[] {terminalEquipmentId })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentStructureModification",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
