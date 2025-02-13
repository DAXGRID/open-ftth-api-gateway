using DAX.ObjectVersioning.Graph;
using FluentResults;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using OpenFTTH.CQRS;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Primitives;
using Roy_T.AStar.Paths;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.QueryHandlers
{
    public class ShortestPathBetweenRouteNodesQueryHandler :
        IQueryHandler<ShortestPathBetweenRouteNodes, Result<ShortestPathBetweenRouteNodesResult>>
    {
        private readonly ILogger<FindNearestRouteNodeQueryHandler> _logger;
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;
        private readonly IRouteNetworkState _routeNetworkState;

        public ShortestPathBetweenRouteNodesQueryHandler(ILoggerFactory loggerFactory, IEventStore eventStore, IRouteNetworkRepository routeNodeRepository, IRouteNetworkState routeNetworkState)
        {
            if (null == loggerFactory)
            {
                throw new ArgumentNullException("loggerFactory is null");
            }

            _logger = loggerFactory.CreateLogger<FindNearestRouteNodeQueryHandler>();

            _eventStore = eventStore;
            _routeNetworkRepository = routeNodeRepository;
            _routeNetworkState = routeNetworkState;
        }

        public Task<Result<ShortestPathBetweenRouteNodesResult>> HandleAsync(ShortestPathBetweenRouteNodes query)
        {
            Stopwatch sw = new();
            sw.Start();

            var sourceNode = _routeNetworkRepository.NetworkState.GetRouteNetworkElement(query.SourceRouteNodeId) as RouteNode;

            if (sourceNode == null)
            {
                return Task.FromResult(
                     Result.Fail<ShortestPathBetweenRouteNodesResult>(new FindNearestRouteNodesError(FindNearestRouteNodesErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network node with id: {query.SourceRouteNodeId}"))
                 );
            }

            var destNode = _routeNetworkRepository.NetworkState.GetRouteNetworkElement(query.DestRouteNodeId) as RouteNode;

            if (destNode == null)
            {
                return Task.FromResult(
                     Result.Fail<ShortestPathBetweenRouteNodesResult>(new FindNearestRouteNodesError(FindNearestRouteNodesErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network node with id: {query.DestRouteNodeId}"))
                 );
            }

            double expandPercent = 20;

            for (int i = 0; i < 10; i++)
            {
                var result = AStartShortestPath(query.SourceRouteNodeId, query.DestRouteNodeId, expandPercent);

                if (result.RouteNetworkElementIds.Count > 1 && result.RouteNetworkElementIds.First() == query.SourceRouteNodeId && result.RouteNetworkElementIds.Last() == query.DestRouteNodeId)
                {
                    return Task.FromResult(
                       Result.Ok<ShortestPathBetweenRouteNodesResult>(
                           result
                       )
                   );
                }

                expandPercent = expandPercent * 2;
            }

            return Task.FromResult(
                    Result.Fail<ShortestPathBetweenRouteNodesResult>(new FindNearestRouteNodesError(FindNearestRouteNodesErrorCodes.ERROR_REACHING_DESTINATION, $"Giving up finding path between node with id: {query.SourceRouteNodeId} and {query.DestRouteNodeId}"))
            );
        }

        private ShortestPathBetweenRouteNodesResult AStartShortestPath(Guid sourceRouteNodeId, Guid destRouteNodeId, double expandPercent)
        {
            Stopwatch st = new Stopwatch();

            st.Start();
            
            var graph = GetGraphForTracing(sourceRouteNodeId, destRouteNodeId, expandPercent);

            var shortestPathResult = graph.ShortestPath(sourceRouteNodeId, destRouteNodeId);

            st.Stop();

            return new ShortestPathBetweenRouteNodesResult(shortestPathResult, st.ElapsedMilliseconds);
        }


        private GraphHolder GetGraphForTracing(Guid sourceRouteNodeId, Guid destRouteNodeId, double expandPercent)
        {
            var extent = new Model.Envelope();

            IRouteNode sourceNode = GetNode(sourceRouteNodeId);
            extent.ExpandToInclude(sourceNode.X, sourceNode.Y);

            IRouteNode destNode = GetNode(destRouteNodeId);
            extent.ExpandToInclude(destNode.X, destNode.Y);


            if (extent.Width < extent.Height)
            {
                extent.ExpandWidth((extent.Height - extent.Width) / 2);
            }
            else if (extent.Width > extent.Height)
            {
                extent.ExpandHeight((extent.Width - extent.Height) / 2);
            }

            // Expand at least 50 meter to prevent small branch cables etc. to be out of extent
            extent.Expand(50);

            // Expand percentage
            extent.ExpandPercent(expandPercent);

            GraphHolder graph = new();

            var version = _routeNetworkState.GetLatestCommitedVersion();

            var routeNetworkCandidates = _routeNetworkRepository.GetByEnvelope(version, extent).ToList();

            foreach (var routeNetworkElement in routeNetworkCandidates)
            {
                if (routeNetworkElement is IRouteNode node)
                {
                    var shortestPathNode = new Node(new Position((float)node.X, (float)node.Y));
                    graph.Nodes.Add(routeNetworkElement.Id, shortestPathNode);
                    graph.NodeToRouteNodeId.Add(shortestPathNode, routeNetworkElement.Id);
                }
            }

            foreach (var routeNetworkElement in routeNetworkCandidates)
            {
                if (routeNetworkElement is RouteSegment segment)
                {
                    // Only add segment if it has never been deleted
                    if (routeNetworkElement.DeletionVersion == null)
                    {
                        var fromNodeId = segment.InV(version).Id;
                        var toNodeId = segment.OutV(version).Id;

                        if (graph.Nodes.TryGetValue(fromNodeId, out Node fromNode))
                        {
                            if (graph.Nodes.TryGetValue(toNodeId, out Node toNode))
                            {
                                var edgeForward = new Edge(fromNode, toNode, Velocity.FromKilometersPerHour(1000));
                                graph.EdgeToRouteSegmentId.Add(edgeForward, segment.Id);

                                var edgeBackward = new Edge(toNode, fromNode, Velocity.FromKilometersPerHour(1000));
                                graph.EdgeToRouteSegmentId.Add(edgeBackward, segment.Id);

                                fromNode.Outgoing.Add(edgeForward);
                                toNode.Incoming.Add(edgeForward);

                                fromNode.Incoming.Add(edgeBackward);
                                toNode.Outgoing.Add(edgeBackward);
                            }
                        }
                    }
                }
            }

            return graph;
        }

        private IRouteNode GetNode(Guid nodeId)
        {
            var sourceNode = _routeNetworkState.GetRouteNetworkElement(nodeId) as IRouteNode;

            if (sourceNode == null)
                throw new ApplicationException($"Cannot find any route node with id: {nodeId} in route network state.");

            return sourceNode;
        }


        class GraphHolder
        {
            public Dictionary<Guid, Node> Nodes = new();
            public Dictionary<IEdge, Guid> EdgeToRouteSegmentId = new();
            public Dictionary<INode, Guid> NodeToRouteNodeId = new();

            public List<Guid> ShortestPath(Guid fromNodeId, Guid toNodeId)
            {
                List<Guid> routeNetworkElementIdsResult = new();

                var pathFinder = new PathFinder();

                var graphFromNode = Nodes[fromNodeId];
                var graphToNode = Nodes[toNodeId];

                var maxAgentSpeed = Velocity.FromKilometersPerHour(1);

                var path = pathFinder.FindPath(graphFromNode, graphToNode, maximumVelocity: maxAgentSpeed);

                if (path != null)
                {
                    IEdge? prevEdge = null;

                    bool first = true;

                    foreach (var currentEdge in path.Edges)
                    {
                        if (prevEdge != null)
                        {
                            var currentStartNodeId = NodeToRouteNodeId[currentEdge.Start];
                            var currentEndNodeId = NodeToRouteNodeId[currentEdge.End];

                            var prevStartNodeId = NodeToRouteNodeId[prevEdge.Start];
                            var prevEndNodeId = NodeToRouteNodeId[prevEdge.End];

                            // (s)prev(e)->(s)current(e)
                            if (prevEndNodeId == currentStartNodeId)
                            {
                                if (first)
                                {
                                    routeNetworkElementIdsResult.Add(prevStartNodeId);
                                    routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[prevEdge]);
                                    routeNetworkElementIdsResult.Add(currentStartNodeId);

                                    first = false;
                                }

                                routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[currentEdge]);
                                routeNetworkElementIdsResult.Add(currentEndNodeId);
                            }
                            // (e)prev(s)->(s)current(e)
                            else if (prevStartNodeId == currentStartNodeId)
                            {
                                if (first)
                                {
                                    routeNetworkElementIdsResult.Add(prevEndNodeId);
                                    routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[prevEdge]);
                                    routeNetworkElementIdsResult.Add(currentStartNodeId);
                                    first = false;
                                }

                                routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[currentEdge]);
                                routeNetworkElementIdsResult.Add(currentEndNodeId);
                            }
                            // (s)prev(e)->(e)current(s)
                            else if (prevEndNodeId == currentEndNodeId)
                            {
                                if (first)
                                {
                                    routeNetworkElementIdsResult.Add(prevStartNodeId);
                                    routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[prevEdge]);
                                    routeNetworkElementIdsResult.Add(currentEndNodeId);
                                    first = false;
                                }

                                routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[currentEdge]);
                                routeNetworkElementIdsResult.Add(currentStartNodeId);
                            }
                            // (e)prev(s)->(e)current(s)
                            else if (prevStartNodeId == currentEndNodeId)
                            {
                                if (first)
                                {
                                    routeNetworkElementIdsResult.Add(prevEndNodeId);
                                    routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[prevEdge]);
                                    routeNetworkElementIdsResult.Add(currentEndNodeId);
                                    first = false;
                                }

                                routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[currentEdge]);
                                routeNetworkElementIdsResult.Add(currentStartNodeId);
                            }

                        }


                        prevEdge = currentEdge;
                    }

                    if (routeNetworkElementIdsResult.Count == 0 && prevEdge != null)
                    {
                        var prevStartNodeId = NodeToRouteNodeId[prevEdge.Start];
                        var prevEndNodeId = NodeToRouteNodeId[prevEdge.End];

                        if (prevStartNodeId == fromNodeId)
                        {
                            routeNetworkElementIdsResult.Add(prevStartNodeId);
                            routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[prevEdge]);
                            routeNetworkElementIdsResult.Add(prevEndNodeId);
                        }
                        else
                        {
                            routeNetworkElementIdsResult.Add(prevEndNodeId);
                            routeNetworkElementIdsResult.Add(EdgeToRouteSegmentId[prevEdge]);
                            routeNetworkElementIdsResult.Add(prevStartNodeId);
                        }
                    }
                }

                return routeNetworkElementIdsResult;
            }
        }
    }

}


