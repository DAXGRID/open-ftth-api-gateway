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
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class RemoveRackFromNodeContainerCommandHandler : ICommandHandler<RemoveRackFromNodeContainer, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public RemoveRackFromNodeContainerCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(RemoveRackFromNodeContainer command)
        {
            var utilityNetworkGraph = _eventStore.Projections.Get<UtilityNetworkProjection>().Graph;
            var nodeContainers = _eventStore.Projections.Get<UtilityNetworkProjection>().NodeContainerByEquipmentId;

            var getNodeContainerResult = QueryHelper.GetNodeContainerFromRouteNodeId(_queryDispatcher, command.RouteNodeId);

            if (getNodeContainerResult.IsFailed)
                return Task.FromResult(Result.Fail(getNodeContainerResult.Errors.First()));

            var nodeContainer = getNodeContainerResult.Value;

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(nodeContainer.Id);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            // Make sure rack id specificed exists
            if (nodeContainer.Racks == null || !nodeContainer.Racks.Any(r => r.Id == command.RackId))
                return Task.FromResult(Result.Fail(new NodeContainerError(NodeContainerErrorCodes.RACK_ID_NOT_FOUND, $"No rack with id: {command.RackId} found")));

            var rack = nodeContainer.Racks.First(r => r.Id == command.RackId);

            List<TerminalEquipmentAR> terminalEquipmentARsToSave = new();

            // First try remnove all equipments within rack
            foreach (var subRackMount in rack.SubrackMounts)
            {
                var terminalEquipmentAR = _eventStore.Aggregates.Load<TerminalEquipmentAR>(subRackMount.TerminalEquipmentId);

                var terminalEquipmentRemoveResult = terminalEquipmentAR.Remove(commandContext, utilityNetworkGraph);

                if (terminalEquipmentRemoveResult.IsFailed)
                    return Task.FromResult(Result.Fail(terminalEquipmentRemoveResult.Errors.First()));

                terminalEquipmentARsToSave.Add(terminalEquipmentAR);
            }

            // Try remove rack itself
            var removeRackResult = nodeContainerAR.RemoveRack(commandContext, command.RackId);

            if (removeRackResult.IsFailed)
                return Task.FromResult(Result.Fail(removeRackResult.Errors.First()));

            // Save terminal equipments
            foreach (var terminalEquipmentAR in terminalEquipmentARsToSave)
            {
                _eventStore.Aggregates.Store(terminalEquipmentAR);
            }

            // Save node container
            _eventStore.Aggregates.Store(nodeContainerAR);


            NotifyExternalServicesAboutChange(nodeContainer);

            return Task.FromResult(Result.Ok());
        }

        private List<SpanEquipment> GetRelatedSpanEquipments(Guid routeNodeId)
        {
            // Get interest information for all equipments in node
            var queryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(new GetRouteNetworkDetails(new RouteNetworkElementIdList() { routeNodeId })).Result;

            if (queryResult.IsFailed)
                throw new ApplicationException($"Got unexpected error result: {queryResult.Errors.First().Message} trying to query interest information for node container and/or span equipment while processing the AffixSpanEquipmentToNodeContainer command");

            if (queryResult.Value.RouteNetworkElements == null)
                throw new ApplicationException($"Got unexpected result querying route node: {routeNodeId} Expected one route node but got null");

            if (queryResult.Value.RouteNetworkElements.Count != 1)
                throw new ApplicationException($"Got unexpected result querying route node: {routeNodeId} Expected one route node but got: {queryResult.Value.RouteNetworkElements.Count}");


            var _utilityNetwork = _eventStore.Projections.Get<UtilityNetworkProjection>();

            List<SpanEquipment> result = new();

            var routeNode = queryResult.Value.RouteNetworkElements.First();

            if (routeNode.InterestRelations != null)
            {
                foreach (var interestRel in routeNode.InterestRelations)
                {
                    // Find span equipment
                    if (_utilityNetwork.TryGetEquipment<SpanEquipment>(interestRel.RefId, out SpanEquipment spanEquipment))
                    {
                        result.Add(spanEquipment);
                    }
                }
            }

            return result;
        }

        private async void NotifyExternalServicesAboutChange(NodeContainer nodeContainer)
        {
            var idChangeSets = new List<IdChangeSet>
            {
                new IdChangeSet("NodeContainer", ChangeTypeEnum.Addition, new Guid[] { nodeContainer.Id })
            };

            var updatedEvent =
                new RouteNetworkElementContainedEquipmentUpdated(
                    eventType: typeof(RouteNetworkElementContainedEquipmentUpdated).Name,
                    eventId: Guid.NewGuid(),
                    eventTimestamp: DateTime.UtcNow,
                    applicationName: "UtilityNetworkService",
                    applicationInfo: null,
                    category: "EquipmentDeletion",
                    idChangeSets: idChangeSets.ToArray(),
                    affectedRouteNetworkElementIds: new Guid[] { nodeContainer.RouteNodeId }
                );

            await _externalEventProducer.Produce(
                nameof(RouteNetworkElementContainedEquipmentUpdated),
                updatedEvent);
        }
    }
}
