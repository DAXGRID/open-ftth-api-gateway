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
    public class FindNearestRouteNodeQueryHandler :
        IQueryHandler<FindNearestRouteNodes, Result<FindNearestRouteNodesResult>>
    {
        private readonly ILogger<FindNearestRouteNodeQueryHandler> _logger;
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNodeRepository;
        private readonly IRouteNetworkState _routeNetworkState;

        public FindNearestRouteNodeQueryHandler(ILoggerFactory loggerFactory, IEventStore eventStore, IRouteNetworkRepository routeNodeRepository, IRouteNetworkState routeNetworkState)
        {
            if (null == loggerFactory)
            {
                throw new ArgumentNullException("loggerFactory is null");
            }

            _logger = loggerFactory.CreateLogger<FindNearestRouteNodeQueryHandler>();

            _eventStore = eventStore;
            _routeNodeRepository = routeNodeRepository;
            _routeNetworkState = routeNetworkState;
        }

        public Task<Result<FindNearestRouteNodesResult>> HandleAsync(FindNearestRouteNodes query)
        {
            Stopwatch sw = new();
            sw.Start();

            var getRouteNetworkElementsResult = _routeNodeRepository.GetRouteElements(new RouteNetworkElementIdList() { query.SourceRouteNodeId });

            // Here we return a error result, because we're dealing with invalid route network ids provided by the client
            if (getRouteNetworkElementsResult.IsFailed || getRouteNetworkElementsResult.Value.Count != 1)
                return Task.FromResult(
                    Result.Fail<FindNearestRouteNodesResult>(new FindNearestRouteNodesError(FindNearestRouteNodesErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network node with id: {query.SourceRouteNodeId}")).
                    WithError(getRouteNetworkElementsResult.Errors.First())
                );

            var sourceRouteNode = getRouteNetworkElementsResult.Value.First() as RouteNode;

            if (sourceRouteNode == null)
                return Task.FromResult(
                    Result.Fail<FindNearestRouteNodesResult>(new FindNearestRouteNodesError(FindNearestRouteNodesErrorCodes.INVALID_QUERY_ARGUMENT_ERROR_LOOKING_UP_SPECIFIED_ROUTE_NETWORK_ELEMENT_BY_ID, $"Error looking up route network node. Got { getRouteNetworkElementsResult.Value.First().GetType().Name} querying element with id: {query.SourceRouteNodeId}")).
                    WithError(getRouteNetworkElementsResult.Errors.First())
                );

            var sourceRouteNodePoint = GetPoint(sourceRouteNode.Coordinates);

            long version = _routeNetworkState.GetLatestCommitedVersion();
            var stopHash = query.NodeKindStops.ToHashSet();
            var interestHash = query.NodeKindOfInterests.ToHashSet();

            // Fetch objects from route network graph to query on
            var routeNetworkSubset = sourceRouteNode.UndirectionalDFS<RouteNode, RouteSegment>(
                version: version, 
                nodeCriteria: n => (n.RouteNodeInfo == null || n.RouteNodeInfo.Kind == null || !stopHash.Contains(n.RouteNodeInfo.Kind.Value)) && GetPoint(n.Coordinates).Distance(sourceRouteNodePoint) < query.SearchRadiusMeters, 
                includeElementsWhereCriteriaIsFalse: true
            );

            // Find nodes to check/trace shortest path
            var nodeCandidates = GetAllNodeCandidates(sourceRouteNode, interestHash, routeNetworkSubset);
            var graphForTracing = GetGraphForTracing(version, nodeCandidates, routeNetworkSubset);
            var nodesOfInterest = GetNodesOfInterest(nodeCandidates, interestHash).ToList();
            
            ConcurrentBag<NearestRouteNodeTraceResult> nodeTraceResults = new();

            int nShortestPathTraces = 0;

            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = Environment.ProcessorCount;


            foreach (var nodeToTrace in nodesOfInterest)
            {
                var shortestPathTrace = ShortestPath(nodeToTrace.Node, sourceRouteNode.Id, graphForTracing);
                nodeTraceResults.Add(shortestPathTrace);
                nShortestPathTraces++;

                if (NumberOfShortestPathTracesWithinDistance(nodeTraceResults, nodeToTrace.BirdDistanceToSource) >= query.MaxHits)
                    break;
            }

            var nodeTraceResultOrdered = nodeTraceResults.OrderBy(n => n.Distance).ToList();

            sw.Stop();
            _logger.LogInformation($"{nShortestPathTraces} shortets path trace(s) processed in {sw.ElapsedMilliseconds} milliseconds finding the {query.MaxHits} nearest nodes to source node with id: {sourceRouteNode.Id}");

            List<NearestRouteNodeTraceResult> tracesToReturn = new();
            List<IRouteNetworkElement> routeNodeElementsToReturn = new();

            for (int i = 0; i < query.MaxHits && i < nodeTraceResultOrdered.Count; i++)
            {
                // Add trace
                var traceToAdd = nodeTraceResultOrdered[i];

                tracesToReturn.Add(traceToAdd);

                var routeElementToAdd = _routeNodeRepository.NetworkState.GetRouteNetworkElement(traceToAdd.DestNodeId);

                if (routeElementToAdd != null)
                    routeNodeElementsToReturn.Add(routeElementToAdd);
            }

            var result = new FindNearestRouteNodesResult(
                sourceRouteNodeId: sourceRouteNode.Id,
                routeNetworkElements: GetRouteNetworkDetailsQueryHandler.MapRouteElementDomainObjectsToQueryObjects(query.RouteNetworkElementFilter, routeNodeElementsToReturn),
                routeNetworkTraces: tracesToReturn
             );

            return Task.FromResult(
                Result.Ok<FindNearestRouteNodesResult>(
                    result
                )
            );
        }

        private int NumberOfShortestPathTracesWithinDistance(IEnumerable<NearestRouteNodeTraceResult> nodeTraceResults, double distance)
        {
            int tracesWithinDistance = 0;

            foreach (var nodeTrace in nodeTraceResults)
            {
                if (nodeTrace.Distance <= distance)
                    tracesWithinDistance++;
            }

            return tracesWithinDistance;
        }

        
        private NearestRouteNodeTraceResult ShortestPath(RouteNode fromNode, Guid toNodeId, GraphHolder graphHolder)
        {
            var pathFinder = new PathFinder();

            var graphFromNode = graphHolder.Nodes[fromNode.Id];
            var graphToNode = graphHolder.Nodes[toNodeId];

            var maxAgentSpeed = Velocity.FromKilometersPerHour(1);

            var path = pathFinder.FindPath(graphFromNode, graphToNode, maximumVelocity: maxAgentSpeed);

            List<Guid> segmentIds = new();
            List<string> segmentGeometries = new();

            double distance = 0;

            if (path != null)
            {
                foreach (var edge in path.Edges)
                {
                    var segment = graphHolder.EdgeToSegment[edge];
                    segmentIds.Add(segment.Id);
                    segmentGeometries.Add(segment.Coordinates);
                    distance += graphHolder.EdgeLengths[segment];
                }
            }
            return new NearestRouteNodeTraceResult(fromNode.Id, fromNode?.NamingInfo?.Name, distance, segmentIds.ToArray(), segmentGeometries.ToArray());
        }
        

        private static IOrderedEnumerable<NodeCandidateHolder> GetNodesOfInterest(IEnumerable<NodeCandidateHolder> nodeCandidates, HashSet<RouteNodeKindEnum> interestHash)
        {
            List<NodeCandidateHolder> nodesToCheck = new();

            foreach (var nodeCandidate in nodeCandidates)
            {
                var routeNode = nodeCandidate.Node;

                if (routeNode != null && routeNode.RouteNodeInfo != null && routeNode.RouteNodeInfo.Kind != null && interestHash.Contains(routeNode.RouteNodeInfo.Kind.Value))
                {
                    nodesToCheck.Add(nodeCandidate);
                }

            }

            return nodesToCheck.OrderBy(n => n.BirdDistanceToSource);
        }

        private static IEnumerable<NodeCandidateHolder> GetAllNodeCandidates(RouteNode sourceRouteNode, HashSet<RouteNodeKindEnum> interestHash, IEnumerable<IGraphObject> traceResult)
        {
            var sourceRouteNodePoint = GetPoint(sourceRouteNode.Coordinates);

            List<NodeCandidateHolder> nodeCandidates = new();

            foreach (var graphObj in traceResult)
            {
                if (graphObj is RouteNode)
                {
                    var routeNode = graphObj as RouteNode;

                    var pnt = GetPoint(routeNode.Coordinates);

                    nodeCandidates.Add(
                        new NodeCandidateHolder()
                        {
                            Node = routeNode,
                            BirdDistanceToSource = pnt.Distance(sourceRouteNodePoint),
                            X = (float)pnt.X,
                            Y = (float)pnt.Y
                        }
                    );

                }
            }

            return nodeCandidates;
        }


        private static double GetLength(string lineStringJson)
        {
            List<Coordinate> coordinates = new();

            var coordPairs = JArray.Parse(lineStringJson);
            foreach (var coordPair in coordPairs)
            {
                coordinates.Add(new Coordinate(((JArray)coordPair)[0].Value<double>(), ((JArray)coordPair)[1].Value<double>()));
            }

            return new LineString(coordinates.ToArray()).Length;
        }

        private static Point GetPoint(string pointGeojson)
        {
            List<Coordinate> coordinates = new();

            var coordPairs = JArray.Parse(pointGeojson);

            return new Point(((JArray)coordPairs)[0].Value<double>(), ((JArray)coordPairs)[1].Value<double>());
        }

        private static GraphHolder GetGraphForTracing(long version, IEnumerable<NodeCandidateHolder> nodeCandidates, IEnumerable<IGraphObject> traceResult)
        {
            GraphHolder result = new();

            var nodeA = new Node(Position.Zero);
            var nodeB = new Node(new Position(10, 10));

            foreach (var node in nodeCandidates)
            {
                result.Nodes.Add(node.Node.Id, new Node(new Position(node.X * 100, node.Y * 100)));
            }

            foreach (var grapObject in traceResult)
            {
                switch (grapObject)
                {
                    case RouteSegment segment:
                        var fromNode = result.Nodes[segment.InV(version).Id];
                        var toNode = result.Nodes[segment.OutV(version).Id];

                        var edgeForward = new Edge(fromNode, toNode, Velocity.FromKilometersPerHour(100));
                        var edgeBackward = new Edge(toNode, fromNode, Velocity.FromKilometersPerHour(100));

                        fromNode.Outgoing.Add(edgeForward);
                        toNode.Incoming.Add(edgeForward);

                        fromNode.Incoming.Add(edgeBackward);
                        toNode.Outgoing.Add(edgeBackward);


                        result.EdgeToSegment.Add(edgeForward, segment);
                        result.EdgeToSegment.Add(edgeBackward, segment);

                        System.Diagnostics.Debug.WriteLine($"{segment.InV(version).Id}->{segment.Id}->{segment.OutV(version).Id}");

                        var length = GetLength(segment.Coordinates);
                        result.EdgeLengths.Add(segment, length);

                        //fromNode.Connect(toNode, Velocity.FromKilometersPerHour(100));
                        break;
                }
            }

            return result;
        }


        class NodeCandidateHolder
        {
            public RouteNode Node { get; set; }
            public double BirdDistanceToSource { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
        }

        class GraphHolder
        {
            public Dictionary<Guid, Node> Nodes { get; set; }
            public Dictionary<IEdge, RouteSegment> EdgeToSegment { get; set; }

            public Dictionary<RouteSegment, double> EdgeLengths { get; set; }

            public GraphHolder()
            {
                Nodes = new();
                EdgeLengths = new();
                EdgeToSegment = new();
            }
        }

    }

}


