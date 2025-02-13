using DAX.ObjectVersioning.Core;
using DAX.ObjectVersioning.Graph;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph.Trace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public class UtilityGraph
    {
        private readonly UtilityNetworkProjection _utilityNetworkProjection;

        private ConcurrentDictionary<Guid, IUtilityGraphElement> _graphElementsById = new ConcurrentDictionary<Guid, IUtilityGraphElement>();

        private InMemoryObjectManager _objectManager = new InMemoryObjectManager();
        
        public long LatestCommitedVersion => _objectManager.GetLatestCommitedVersion();

        public UtilityGraph(UtilityNetworkProjection utilityNetworkProjection)
        {
            _utilityNetworkProjection = utilityNetworkProjection;
        }

        public bool TryGetGraphElement<T>(Guid id, out T utilityGraphElement) where T : IUtilityGraphElement
        {
            if (_graphElementsById.TryGetValue(id, out var graphElement))
            {
                if (graphElement is T)
                {
                    utilityGraphElement = (T)graphElement;
                    return true;
                }
            }

            #pragma warning disable CS8601 // Possible null reference assignment.
            utilityGraphElement = default(T);
            #pragma warning restore CS8601 // Possible null reference assignment.

            return false;
        }

        #region Simple Trace       

        public UtilityGraphTraceResult SimpleTrace(Guid id)
        {

            if (_graphElementsById.TryGetValue(id, out var utilityGraphElement))
            {
                if (utilityGraphElement is UtilityGraphDisconnectedSegment)
                {
                    return new UtilityGraphTraceResult(id, null, Array.Empty<IGraphObject>(), Array.Empty<IGraphObject>());
                }
                else if (utilityGraphElement is UtilityGraphConnectedSegment)
                {
                    var connectedSegment = (UtilityGraphConnectedSegment)utilityGraphElement;

                    var version = _objectManager.GetLatestCommitedVersion();

                    var upstream = UpstreamSegmentTrace(connectedSegment, version).ToArray();
                    var downstream = DownstreamSegmentTrace(connectedSegment, version).ToArray();

                    return new UtilityGraphTraceResult(id, connectedSegment, downstream, upstream);
                }
                else if (utilityGraphElement is UtilityGraphConnectedTerminal)
                {
                    IUtilityGraphTerminalRef terminalRef = (UtilityGraphConnectedTerminal)utilityGraphElement;

                    var version = _objectManager.GetLatestCommitedVersion();

                    var graphTerminal = (UtilityGraphConnectedTerminal)_objectManager.GetObject(terminalRef.TerminalId);

                    var terminal = graphTerminal.Terminal(_utilityNetworkProjection);

                    if (graphTerminal != null)
                    {
                        var nSegmentNeigbours = graphTerminal.NeighborElements(version).Where(n => n.GetType() != typeof(UtilityGraphInternalEquipmentConnectivityLink)).Count();

                        if (nSegmentNeigbours == 1)
                        {
                            if (terminal.Direction == TerminalDirectionEnum.OUT)
                            {
                                var downstream = DownstreamTerminalTrace(graphTerminal, version).ToArray();
                                return new UtilityGraphTraceResult(id, graphTerminal, downstream, Array.Empty<IGraphObject>());
                            }
                            else
                            {
                                var upstream = UpstreamTerminalTrace(graphTerminal, version).ToArray();
                                return new UtilityGraphTraceResult(id, graphTerminal, Array.Empty<IGraphObject>(), upstream);
                            }
                        }
                        else if (nSegmentNeigbours == 2)
                        {
                            var upstream = UpstreamTerminalTrace(graphTerminal, version).ToArray();
                            var downstream = DownstreamTerminalTrace(graphTerminal, version).ToArray();

                            return new UtilityGraphTraceResult(id, graphTerminal, downstream, upstream);
                        }
                        else if (nSegmentNeigbours > 2)
                        {
                            throw new ApplicationException($"terminal with id: {graphTerminal.Id} version: {version} have more than two segment connected to it. The system must prevent that to never happend!");
                        }
                    }
                }
            }

            return new UtilityGraphTraceResult(id, null, Array.Empty<IGraphObject>(), Array.Empty<IGraphObject>());
        }

        private List<IGraphObject> DownstreamSegmentTrace(UtilityGraphConnectedSegment connectedSegment, long version, bool traceThroughSplitters = false, bool filterOutput = true)
        {
            SimpleTraceHelper terminalTracker = new(_utilityNetworkProjection, version, traceThroughSplitters);

            var downstreamTrace = connectedSegment.UndirectionalDFS<GraphNode, GraphEdge>(
                version,
                n => n != connectedSegment.OutV(version) && terminalTracker.Add(n),
                e => terminalTracker.ContinueTrace(e)
            ).ToList();

            var lastDownstreamObject = downstreamTrace.Last();

            if (lastDownstreamObject is UtilityGraphConnectedSegment lastDownstreamSegment)
            {
                if (lastDownstreamSegment.InV(version) == null)
                {
                    downstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastDownstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastDownstreamSegment.SpanSegment(_utilityNetworkProjection).FromNodeOfInterestIndex]));
                }
                else if (lastDownstreamSegment.OutV(version) == null)
                {
                    downstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastDownstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastDownstreamSegment.SpanSegment(_utilityNetworkProjection).ToNodeOfInterestIndex]));
                }
                else
                {
                    throw new ApplicationException($"Last element in downstream trace was a UtilityGraphConnectedSegment with id: {lastDownstreamSegment.Id}, not a terminal. However, the segment seems to have to have an downstream terminal connection. Something wrong!");
                }
            }

            if (filterOutput)
                return terminalTracker.FilterUnrelevantElementsAway(downstreamTrace);
            else
                return downstreamTrace;
        }

        private List<IGraphObject> DownstreamTerminalTrace(UtilityGraphConnectedTerminal terminal, long version, bool traceThroughSplitters = false, bool filterOutput = true)
        {
            SimpleTraceHelper terminalTracker = new(_utilityNetworkProjection, version, traceThroughSplitters);

            var lastSegment = terminal.NeighborElements(version).Where(n => n.GetType() != typeof(UtilityGraphInternalEquipmentConnectivityLink)).Last();

            var downstreamTrace = lastSegment.UndirectionalDFS<GraphNode, GraphEdge>(
                version,
                n => n != terminal && terminalTracker.Add(n),
                e => terminalTracker.ContinueTrace(e)
            ).ToList();

            var lastDownstreamObject = downstreamTrace.Last();

            if (lastDownstreamObject is UtilityGraphConnectedSegment lastDownstreamSegment)
            {
                if (lastDownstreamSegment.InV(version) == null)
                {
                    downstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastDownstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastDownstreamSegment.SpanSegment(_utilityNetworkProjection).FromNodeOfInterestIndex]));
                }
                else if (lastDownstreamSegment.OutV(version) == null)
                {
                    downstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastDownstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastDownstreamSegment.SpanSegment(_utilityNetworkProjection).ToNodeOfInterestIndex]));
                }
                else
                {
                    throw new ApplicationException($"Last element in downstream trace was a UtilityGraphConnectedSegment with id: {lastDownstreamSegment.Id}, not a terminal. However, the segment seems to have to have an downstream terminal connection. Something wrong!");
                }
            }

            if (filterOutput)
                return terminalTracker.FilterUnrelevantElementsAway(downstreamTrace);
            else
                return downstreamTrace;
        }

        private List<IGraphObject> UpstreamSegmentTrace(UtilityGraphConnectedSegment connectedSegment, long version, bool traceThroughSplitters = false, bool filterOutput = true)
        {
            SimpleTraceHelper terminalTracker = new(_utilityNetworkProjection, version, traceThroughSplitters);

            var upstreamTrace = connectedSegment.UndirectionalDFS<GraphNode, GraphEdge>(
                version,
                n => n != connectedSegment.InV(version) && terminalTracker.Add(n),
                e => terminalTracker.ContinueTrace(e)
            ).ToList();

            var lastUpstreamObject = upstreamTrace.Last();

            if (lastUpstreamObject is UtilityGraphConnectedSegment lastUpstreamSegment)
            {
                if (lastUpstreamSegment.OutV(version) == null)
                {
                    upstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastUpstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastUpstreamSegment.SpanSegment(_utilityNetworkProjection).ToNodeOfInterestIndex]));
                }
                else if (lastUpstreamSegment.InV(version) == null)
                {
                    upstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastUpstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastUpstreamSegment.SpanSegment(_utilityNetworkProjection).FromNodeOfInterestIndex]));
                }
                else
                {
                    throw new ApplicationException($"Last element in upstream trace was a UtilityGraphConnectedSegment with id: {lastUpstreamSegment.Id}, not a terminal. However, the segment seems to have to have an upstream terminal connection. Something wrong!");
                }
            }

            if (traceThroughSplitters)
                terminalTracker.UpstreamTrace = true;

            if (filterOutput)
                return terminalTracker.FilterUnrelevantElementsAway(upstreamTrace);
            else
                return upstreamTrace;
        }

        private List<IGraphObject> UpstreamTerminalTrace(UtilityGraphConnectedTerminal terminal, long version, bool traceThroughSplitters = false, bool filterOutput = true)
        {
            SimpleTraceHelper terminalTracker = new(_utilityNetworkProjection, version, traceThroughSplitters);

            var firstSegment = terminal.NeighborElements(version).Where(n => n.GetType() != typeof(UtilityGraphInternalEquipmentConnectivityLink)).First();

            var upstreamTrace = firstSegment.UndirectionalDFS<GraphNode, GraphEdge>(
                version,
                n => n != terminal && terminalTracker.Add(n),
                e => terminalTracker.ContinueTrace(e)
             ).ToList();

            var lastUpstreamObject = upstreamTrace.Last();

            if (lastUpstreamObject is UtilityGraphConnectedSegment lastUpstreamSegment)
            {
                if (lastUpstreamSegment.OutV(version) == null)
                {
                    upstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastUpstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastUpstreamSegment.SpanSegment(_utilityNetworkProjection).ToNodeOfInterestIndex]));
                }
                else if (lastUpstreamSegment.InV(version) == null)
                {
                    upstreamTrace.Add(new UtilityGraphConnectedTerminal(Guid.Empty, Guid.Empty, lastUpstreamSegment.SpanEquipment(_utilityNetworkProjection).NodesOfInterestIds[lastUpstreamSegment.SpanSegment(_utilityNetworkProjection).FromNodeOfInterestIndex]));
                }
                else
                {
                    throw new ApplicationException($"Last element in upstream trace was a UtilityGraphConnectedSegment with id: {lastUpstreamSegment.Id}, not a terminal. However, the segment seems to have to have an upstream terminal connection. Something wrong!");
                }
            }

            if (traceThroughSplitters)
                terminalTracker.UpstreamTrace = true;

            if (filterOutput)
                return terminalTracker.FilterUnrelevantElementsAway(upstreamTrace);
            else
                return upstreamTrace;
        }

        private class SimpleTraceHelper
        {
            private readonly UtilityNetworkProjection _utilityNetworkProjection;
            private readonly long _version;
            private readonly bool _traceThroughSplitters;
            private bool _upstreamTrace;

            HashSet<UtilityGraphConnectedTerminal> visited = new();

            public IGraphNode LastVisit = null;

            public bool UpstreamTrace {
                get { return _upstreamTrace; }

                set { _upstreamTrace = value; }
            }

            public SimpleTraceHelper(UtilityNetworkProjection utilityNetworkProjection, long version, bool traceThroughSplitters = false)
            {
                _utilityNetworkProjection = utilityNetworkProjection;
                _version = version;
                _traceThroughSplitters = traceThroughSplitters;
            }

            public bool Add(IGraphNode node)
            {
                if (visited.Contains(node))
                {
                    return false;
                }
                else
                {
                    LastVisit = node;
                    return true;
                }

            }

            public bool ContinueTrace(GraphEdge e)
            {
                var edgeType = e.GetType();

                if (LastVisit != null && LastVisit is UtilityGraphConnectedTerminal && !((UtilityGraphConnectedTerminal)LastVisit).IsSimpleTerminal)
                {
                    var lastTerminal = ((UtilityGraphConnectedTerminal)LastVisit).Terminal(_utilityNetworkProjection);

                    var lastEq = ((UtilityGraphConnectedTerminal)LastVisit).TerminalEquipment(_utilityNetworkProjection);

                    System.Diagnostics.Debug.WriteLine("TRACE STEP: " + lastEq.Name + " - " + lastTerminal.Name);
                }

                // If trace through splitters disallow tracing upstream a splitter
                if (_traceThroughSplitters)
                {
                    if (edgeType == typeof(UtilityGraphInternalEquipmentConnectivityLink) && LastVisit != null && LastVisit is UtilityGraphConnectedTerminal && !((UtilityGraphConnectedTerminal)LastVisit).IsSimpleTerminal)
                    {
                        var lastTerminal = ((UtilityGraphConnectedTerminal)LastVisit).Terminal(_utilityNetworkProjection);

                        // if we're comming from an out to an internal link, we're tracing upstream which we don't allow in splitter trace
                        if (lastTerminal.Direction == TerminalDirectionEnum.OUT)
                            return false;
                    }

                    return true;
                }

                if (edgeType == typeof(UtilityGraphConnectedSegment))
                    return true;

                if (edgeType == typeof(UtilityGraphTerminalToTerminalConnectivityLink))
                    return true;


                if (edgeType == typeof(UtilityGraphInternalEquipmentConnectivityLink))
                {
                    // If we're comming from an internal connectivity node
                    if (LastVisit != null && LastVisit is UtilityGraphInternalEquipmentConnectivityNode)
                    {
                        // If edge out is connected to last node, we're tracing upstream a splitter, which is allowed
                        if (e.OutV(_version) == LastVisit)
                            return true;
                    }

                    // if the last node is an out terminal, then we're tracing upstream, which is allowed
                    if (LastVisit != null && LastVisit is UtilityGraphConnectedTerminal && !((UtilityGraphConnectedTerminal)LastVisit).IsSimpleTerminal)
                    {
                        var lastTerminal = ((UtilityGraphConnectedTerminal)LastVisit).Terminal(_utilityNetworkProjection);

                        if (lastTerminal.Direction == TerminalDirectionEnum.OUT)
                        {
                            _upstreamTrace = true;
                            return true;
                        }
                        else
                        {
                            _upstreamTrace = false;
                        }
                    }
                }


                return false;
            }

            public List<IGraphObject> FilterUnrelevantElementsAway(List<IGraphObject> upstreamTrace)
            {
                List<IGraphObject> result = new();

                foreach (var graphElement in upstreamTrace)
                {
                    // Filter ways terminal internal connections
                    if (graphElement is UtilityGraphInternalEquipmentConnectivityNode || graphElement is UtilityGraphInternalEquipmentConnectivityLink)
                        continue;

                    // If downstream trace filter away out terminals
                    if (!_upstreamTrace && graphElement is UtilityGraphConnectedTerminal && !((UtilityGraphConnectedTerminal)graphElement).IsSimpleTerminal && ((UtilityGraphConnectedTerminal)graphElement).Terminal(_utilityNetworkProjection).Direction == TerminalDirectionEnum.OUT)
                        continue;

                    // If upstream trace filter away in terminals
                    if (_upstreamTrace && graphElement is UtilityGraphConnectedTerminal && !((UtilityGraphConnectedTerminal)graphElement).IsSimpleTerminal && ((UtilityGraphConnectedTerminal)graphElement).Terminal(_utilityNetworkProjection).Direction == TerminalDirectionEnum.IN)
                        continue;


                    result.Add(graphElement);
                }

                return result;
            }
        }


        #endregion

        #region Trace used in outage view
        public UtilityGraphTraceResult OutageViewTrace(Guid id)
        {
            bool traceThroughSplitters = true;
            bool filterOutput = false;

            if (_graphElementsById.TryGetValue(id, out var utilityGraphElement))
            {
                if (utilityGraphElement is UtilityGraphDisconnectedSegment)
                {
                    return new UtilityGraphTraceResult(id, null, Array.Empty<IGraphObject>(), Array.Empty<IGraphObject>());
                }
                else if (utilityGraphElement is UtilityGraphConnectedSegment)
                {
                    var connectedSegment = (UtilityGraphConnectedSegment)utilityGraphElement;

                    var version = _objectManager.GetLatestCommitedVersion();

                    var upstream = UpstreamSegmentTrace(connectedSegment, version, traceThroughSplitters, filterOutput).ToArray();
                    var downstream = DownstreamSegmentTrace(connectedSegment, version, traceThroughSplitters, filterOutput).ToArray();

                    return new UtilityGraphTraceResult(id, connectedSegment, downstream, upstream);
                }
                else if (utilityGraphElement is IUtilityGraphTerminalRef)
                {
                    IUtilityGraphTerminalRef terminalRef = (IUtilityGraphTerminalRef)utilityGraphElement;

                    var version = _objectManager.GetLatestCommitedVersion();

                    var terminal = (UtilityGraphConnectedTerminal)_objectManager.GetObject(terminalRef.TerminalId);

                    if (terminal != null)
                    {
                        var nTerminalNeigbours = terminal.NeighborElements(version).Where(n => n.GetType() != typeof(UtilityGraphInternalEquipmentConnectivityLink)).Count();

                        if (nTerminalNeigbours == 1)
                        {
                            var upstream = UpstreamTerminalTrace(terminal, version, traceThroughSplitters, filterOutput).ToArray();
                            return new UtilityGraphTraceResult(id, terminal, Array.Empty<IGraphObject>(), upstream);
                        }
                        else if (nTerminalNeigbours == 2)
                        {
                            var upstream = UpstreamTerminalTrace(terminal, version, traceThroughSplitters, filterOutput).ToArray();
                            var downstream = DownstreamTerminalTrace(terminal, version, traceThroughSplitters).ToArray();

                            return new UtilityGraphTraceResult(id, terminal, downstream, upstream);
                        }
                        else if (nTerminalNeigbours > 2)
                        {
                            throw new ApplicationException($"terminal with id: {terminal.Id} version: {version} have more than two segment connected to it. The system must prevent that to never happend!");
                        }
                    }
                }
            }

            return new UtilityGraphTraceResult(id, null, Array.Empty<IGraphObject>(), Array.Empty<IGraphObject>());
        }

        #endregion



        internal IUtilityGraphTerminalRef? GetTerminal(Guid terminalId, long version)
        {
            // Find or create terminal
            return _objectManager.GetObject(terminalId, version) as IUtilityGraphTerminalRef;
        }

        internal void AddDisconnectedSegment(SpanEquipment spanEquipment, UInt16 structureIndex, UInt16 segmentIndex)
        {
            var spanSegment = spanEquipment.SpanStructures[structureIndex].SpanSegments[segmentIndex];

            var disconnectedGraphSegment = new UtilityGraphDisconnectedSegment(spanEquipment.Id, structureIndex, segmentIndex);

            if (!_graphElementsById.TryAdd(spanSegment.Id, disconnectedGraphSegment))
                throw new ArgumentException($"A span segment with id: {spanSegment.Id} already exists in the graph.");
        }

        internal void AddDisconnectedTerminal(Guid routeNodeId, TerminalEquipment terminalEquipment, Guid terminalId, UInt16 structureIndex, UInt16 terminalIndex)
        {
            var terminal = terminalEquipment.TerminalStructures[structureIndex].Terminals[terminalIndex];

            var disconnectedGraphTerminal = new UtilityGraphDisconnectedTerminal(routeNodeId, terminalId, terminalEquipment.Id, structureIndex, terminalIndex);

            if (!_graphElementsById.TryAdd(terminal.Id, disconnectedGraphTerminal))
                throw new ArgumentException($"A terminal with id: {terminal.Id} already exists in the graph.");
        }

        internal void RemoveGraphElement(Guid graphElementId)
        {
            if (!_graphElementsById.TryRemove(graphElementId, out _))
                throw new ArgumentException($"The graph element with id: {graphElementId} cannot be removed from the graph.");
        }

        internal void UpdateIndex(Guid graphElementId, IUtilityGraphElement newUtilityGraphElementRef)
        {
            RemoveFromIndex(graphElementId);
            AddToIndex(graphElementId, newUtilityGraphElementRef);
        }

        internal void RemoveFromIndex(Guid segmentId)
        {
            if (!_graphElementsById.TryRemove(segmentId, out _))
                throw new ApplicationException($"Cannot remove graph element with id: {segmentId} from graph.");
        }

        internal void AddToIndex(Guid graphElementId, IUtilityGraphElement newUtilityGraphElement)
        {
            if (!_graphElementsById.TryAdd(graphElementId, newUtilityGraphElement))
                throw new ArgumentException($"A graph element with id: {graphElementId} already exists in the graph.");
        }

        internal ITransaction CreateTransaction()
        {
            return _objectManager.CreateTransaction();
        }
    }
}
