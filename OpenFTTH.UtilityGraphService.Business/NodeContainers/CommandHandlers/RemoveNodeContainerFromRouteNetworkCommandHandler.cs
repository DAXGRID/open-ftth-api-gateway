using DAX.EventProcessing;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.Changes;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using OpenFTTH.UtilityGraphService.Business.NodeContainers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.Interest;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class RemoveNodeContainerFromRouteNetworkCommandHandler : ICommandHandler<RemoveNodeContainerFromRouteNetwork, Result>
    {
        private readonly IEventStore _eventStore;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly IExternalEventProducer _externalEventProducer;

        public RemoveNodeContainerFromRouteNetworkCommandHandler(IEventStore eventStore, IQueryDispatcher queryDispatcher, IExternalEventProducer externalEventProducer)
        {
            _eventStore = eventStore;
            _queryDispatcher = queryDispatcher;
            _externalEventProducer = externalEventProducer;
        }

        public Task<Result> HandleAsync(RemoveNodeContainerFromRouteNetwork command)
        {
            var nodeContainers = _eventStore.Projections.Get<UtilityNetworkProjection>().NodeContainerByEquipmentId;

            if (!nodeContainers.TryGetValue(command.NodeContainerId, out var nodeContainer))
            {
                return Task.FromResult(Result.Fail(new RemoveNodeContainerFromRouteNetworkError(RemoveNodeContainerFromRouteNetworkErrorCodes.NODE_CONTAINER_NOT_FOUND, $"Cannot find any node container with id: {command.NodeContainerId}")));
            }

            var relatedSpanEquipments = GetRelatedSpanEquipments(nodeContainer.RouteNodeId);

            var nodeContainerAR = _eventStore.Aggregates.Load<NodeContainerAR>(command.NodeContainerId);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var removeNodeContainerResult = nodeContainerAR.Remove(commandContext, relatedSpanEquipments);

            if (removeNodeContainerResult.IsSuccess)
            {
                // Remember to remove the walk of interest as well
                var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

                var interestAR = _eventStore.Aggregates.Load<InterestAR>(nodeContainer.InterestId);

                OpenFTTH.RouteNetwork.Business.CommandContext routeNetworkCommandContext = new RouteNetwork.Business.CommandContext(commandContext.CorrelationId, commandContext.CmdId, commandContext.UserContext);
              
                var unregisterInterestResult = interestAR.UnregisterInterest(routeNetworkCommandContext, interestProjection, nodeContainer.InterestId);

                if (unregisterInterestResult.IsFailed)
                    throw new ApplicationException($"Failed to unregister interest: {nodeContainer.InterestId} of node container: {nodeContainer.Id} in RemoveNodeContainerFromRouteNetworkCommandHandler Error: {unregisterInterestResult.Errors.First().Message}");

                _eventStore.Aggregates.Store(interestAR);
                _eventStore.Aggregates.Store(nodeContainerAR);

                NotifyExternalServicesAboutChange(nodeContainer);
            }

            return Task.FromResult(removeNodeContainerResult);
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
