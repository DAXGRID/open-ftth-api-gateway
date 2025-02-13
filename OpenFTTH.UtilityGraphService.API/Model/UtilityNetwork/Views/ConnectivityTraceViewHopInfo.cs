using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Represents a line for displaying a hop in a connectivity trace view
    /// </summary>
    public record ConnectivityTraceViewHopInfo
    {
        public int HopSeqNo { get; }
        public int Level { get; }
        public bool IsSplitter { get; }
        public bool IsLineTermination { get; }
        public bool IsCustomerSplitter { get; }
        public bool IsTraceSource { get; }
        public string Node { get; }
        public string Equipment { get; }
        public string TerminalStructure { get; }
        public string Terminal { get; }
        public string ConnectionInfo { get; }
        public double TotalLength { get; }
        public Guid[] RouteSegmentIds { get; }
        public string[] RouteSegmentGeometries { get; }

        public ConnectivityTraceViewHopInfo(int hopSeqNo, int level, bool isSplitter, bool isLineTermination, bool isCustomerSplitter, bool isTraceSource, string node, string equipment, string terminalStructure, string terminal, string connectionInfo, double totalLength, Guid[] routeSegmentIds, string[] routeSegmentGeometries)
        {
            HopSeqNo = hopSeqNo;
            Level = level;
            IsSplitter = isSplitter;
            IsLineTermination = isLineTermination;
            IsCustomerSplitter = isCustomerSplitter;
            IsTraceSource = isTraceSource;
            Node = node;
            Equipment = equipment;
            TerminalStructure = terminalStructure;
            Terminal = terminal;
            ConnectionInfo = connectionInfo;
            TotalLength = totalLength;
            RouteSegmentIds = routeSegmentIds;
            RouteSegmentGeometries = routeSegmentGeometries;
        }
    }
}
