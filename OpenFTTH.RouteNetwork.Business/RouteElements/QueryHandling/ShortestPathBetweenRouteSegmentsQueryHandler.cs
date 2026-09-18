using DAX.ObjectVersioning.Graph;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.RouteNetwork.Infos;
using OpenFTTH.Results;
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
    public class ShortestPathBetweenRouteSegmentsQueryHandler : IQueryHandler<ShortestPathBetweenRouteSegments, Result<ShortestPathBetweenRouteSegmentsResult>>
    {
        private readonly ILogger<ShortestPathBetweenRouteSegmentsQueryHandler> _logger;
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;
        private readonly IRouteNetworkState _routeNetworkState;

        public ShortestPathBetweenRouteSegmentsQueryHandler(ILoggerFactory loggerFactory, IEventStore eventStore, IRouteNetworkRepository routeNetworkRepository, IRouteNetworkState routeNetworkState)
        {
            if (null == loggerFactory)
            {
                throw new ArgumentNullException("loggerFactory is null");
            }

            _logger = loggerFactory.CreateLogger<ShortestPathBetweenRouteSegmentsQueryHandler>();

            _eventStore = eventStore;
            _routeNetworkRepository = routeNetworkRepository;
            _routeNetworkState = routeNetworkState;
        }

        public Task<Result<ShortestPathBetweenRouteSegmentsResult>> HandleAsync(ShortestPathBetweenRouteSegments query)
        {
            Stopwatch sw = new();
            sw.Start();

            var latestVersion = _routeNetworkState.GetLatestCommitedVersion();

            var sourceSegment = _routeNetworkState.GetRouteNetworkElement(query.SourceRouteSegmentId) as RouteSegment;
            if (sourceSegment == null)
            {
                return Task.FromResult(
                     Result.Fail<ShortestPathBetweenRouteSegmentsResult>(new FindNearestRouteSegmentsError(FindNearestRouteSegmentsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network segment with id: {query.SourceRouteSegmentId}"))
                 );
            }

            var sourceSegmentNeighborElements = sourceSegment.NeighborElements(latestVersion);
            var sourceNode = sourceSegmentNeighborElements.First() as RouteNode;

            if (sourceNode == null)
            {
                return Task.FromResult(
                     Result.Fail<ShortestPathBetweenRouteSegmentsResult>(new FindNearestRouteSegmentsError(FindNearestRouteSegmentsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network node with id: {sourceNode.Id}"))
                 );
            }

            var destSegment = _routeNetworkState.GetRouteNetworkElement(query.DestRouteSegmentId) as RouteSegment;
            if (destSegment == null)
            {
                return Task.FromResult(
                     Result.Fail<ShortestPathBetweenRouteSegmentsResult>(new FindNearestRouteSegmentsError(FindNearestRouteSegmentsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network segment with id: {query.DestRouteSegmentId}"))
                 );
            }

            var destSegmentNeighborElements = destSegment.NeighborElements(latestVersion);
            var destNode = destSegmentNeighborElements.Last() as RouteNode;

            if (destNode == null)
            {
                return Task.FromResult(
                     Result.Fail<ShortestPathBetweenRouteSegmentsResult>(new FindNearestRouteSegmentsError(FindNearestRouteSegmentsErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network node with id: {destNode.Id}"))
                 );
            }

            // If nodes are shared they're next to each other so no reason to do a trace.
            if (sourceSegmentNeighborElements.Select(x => x.Id).ToHashSet().Overlaps(destSegmentNeighborElements.Select(x => x.Id)))
            {
                return Task.FromResult(
                    Result.Ok<ShortestPathBetweenRouteSegmentsResult>(
                        new ShortestPathBetweenRouteSegmentsResult(new List<Guid> { sourceNode.Id, sourceSegment.Id, destSegment.Id, destNode.Id }, new List<Guid> { sourceSegment.Id, destSegment.Id }, 0)
                    )
                );
            }

            double expandPercent = 20;

            for (int i = 0; i < 10; i++)
            {
                var result = AStartShortestPath(sourceNode.Id, destNode.Id, expandPercent);

                if (result.RouteNetworkElementIds.Count > 1 && result.RouteNetworkElementIds.First() == sourceNode.Id && result.RouteNetworkElementIds.Last() == destNode.Id)
                {
                    // This is to always include the source segment, we cannot be sure of start or end node, so it might get removed.
                    if (!result.RouteNetworkElementIds.Contains(sourceSegment.Id))
                    {
                        result.RouteNetworkElementIds.Insert(0, sourceSegment.Id);
                        result.RouteSegmentElementIds.Insert(0, sourceSegment.Id);
                    }

                    // This is to always include the dest segment, we cannot be sure of start or end node, so it might get removed.
                    if (!result.RouteNetworkElementIds.Contains(destSegment.Id))
                    {
                        result.RouteNetworkElementIds.Add(destSegment.Id);
                        result.RouteSegmentElementIds.Add(destSegment.Id);
                    }

                    return Task.FromResult(
                        Result.Ok<ShortestPathBetweenRouteSegmentsResult>(
                            result
                        )
                    );
                }

                expandPercent = expandPercent * 2;
            }

            return Task.FromResult(Result.Fail<ShortestPathBetweenRouteSegmentsResult>(new FindNearestRouteSegmentsError(FindNearestRouteSegmentsErrorCodes.ERROR_REACHING_DESTINATION, $"Giving up finding path between node with id: {sourceNode.Id} and {destNode.Id}"))
            );
        }

        private ShortestPathBetweenRouteSegmentsResult AStartShortestPath(Guid sourceRouteNodeId, Guid destRouteNodeId, double expandPercent)
        {
            Stopwatch st = new Stopwatch();

            st.Start();

            var graph = GetGraphForTracing(sourceRouteNodeId, destRouteNodeId, expandPercent);

            var shortestPathResult = graph.ShortestPath(sourceRouteNodeId, destRouteNodeId);

            var routeElementIdList = new RouteNetworkElementIdList();
            routeElementIdList.AddRange(shortestPathResult);
            var validateWalk = new ValidatedRouteNetworkWalk(routeElementIdList);

            st.Stop();

            return new ShortestPathBetweenRouteSegmentsResult(shortestPathResult, validateWalk.SegmentIds, st.ElapsedMilliseconds);
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

