using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class ConnectTerminalsCommandHandler : ICommandHandler<ConnectTerminalsAtRouteNode, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        private readonly UtilityNetworkProjection _utilityNetwork;

        public ConnectTerminalsCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;

            _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();
        }

        public Task<Result> HandleAsync(ConnectTerminalsAtRouteNode command)
        {
            var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(_queryDispatcher, command.RouteNodeId);

            if (getNodeContainerResult.IsFailed)
                return Task.FromResult(Result.Fail(getNodeContainerResult.Errors.First()));

            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(command.FromTerminalId, out var fromTerminalRef))
                return Task.FromResult(Result.Fail(new ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes.TERMINAL_ID_NOT_FOUND, $"Cannot find any terminal in the utility graph with id: {command.FromTerminalId}")));

            if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphTerminalRef>(command.ToTerminalId, out var toTerminalRef))
                return Task.FromResult(Result.Fail(new ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes.TERMINAL_ID_NOT_FOUND, $"Cannot find any terminal in the utility graph with id: {command.ToTerminalId}")));

            var nodeContainer = getNodeContainerResult.Value;

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(nodeContainer.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var placeRackResult = nodeContainerAR.ConnectTerminals(
                commandContext,
                _utilityNetwork.Graph,
                fromTerminalRef.TerminalEquipment(_utilityNetwork),
                command.FromTerminalId,
                toTerminalRef.TerminalEquipment(_utilityNetwork),
                command.ToTerminalId,
                command.FiberCoordLength
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
                    category: "FiberConnectivityModification.Connect",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { routeNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
