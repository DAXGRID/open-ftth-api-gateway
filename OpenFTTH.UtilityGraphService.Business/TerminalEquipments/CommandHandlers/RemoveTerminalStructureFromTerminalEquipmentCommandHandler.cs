using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.CommandHandlers
{
    public class RemoveTerminalStructureFromTerminalEquipmentCommandHandler : ICommandHandler<RemoveTerminalStructureFromTerminalEquipment, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public RemoveTerminalStructureFromTerminalEquipmentCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(RemoveTerminalStructureFromTerminalEquipment command)
        {
            var utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            if (!utilityNetwork.TryGetEquipment<TerminalEquipment>(command.TerminalEquipmentId, out var terminalEquipment))
            {
                return Task.FromResult(Result.Fail(new TerminalEquipmentError(TerminalEquipmentErrorCodes.TERMINAL_EQUIPMENT_NOT_FOUND, $"Cannot find any terminal equipment with id: {command.TerminalEquipmentId}")));
            }
      
            var terminalEquipmentAR = _eventStore.Aggregates.Load<TerminalEquipmentAR>(command.TerminalEquipmentId);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Remove the equipment
            var removeEquipmentStructureResult = terminalEquipmentAR.RemoveStructure(commandContext, command.TerminalStructureId, utilityNetwork.Graph);

            if (removeEquipmentStructureResult.IsFailed)
            {
                return Task.FromResult(removeEquipmentStructureResult);
            }

            if (removeEquipmentStructureResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(terminalEquipmentAR);

                NotifyExternalServicesAboutChange(command.RouteNodeId, terminalEquipment.Id);
            }

            return Task.FromResult(removeEquipmentStructureResult);
        }
      

        private async void NotifyExternalServicesAboutChange(Guid routeNodeId, Guid terminalEquipmentId)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("TerminalEquipment", ChangeTypeEnum.Modification, new Guid[] { terminalEquipmentId })
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
