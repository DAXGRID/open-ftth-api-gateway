using DAX.ObjectVersioning.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.Graph.Trace
{
    public record UtilityGraphTraceResult
    {
        public Guid TerminalOrSpanSegmentId { get; }
        public IGraphObject? Source { get; }
        public IGraphObject[] Downstream { get; }
        public IGraphObject[] Upstream { get; }

        public UtilityGraphTraceResult(Guid terminalOrSpanSegmentId, IGraphObject? source, IGraphObject[] downstream, IGraphObject[] upstream)
        {
            TerminalOrSpanSegmentId = terminalOrSpanSegmentId;
            Source = source;
            Downstream = downstream;
            Upstream = upstream;
        }

        public List<IGraphObject> All
        {
            get
            {
                List<IGraphObject> result = new();

                // First add upstreams in reverse
                foreach (var graphElement in Upstream.Reverse())
                {
                    result.Add(graphElement);
                }

                //  add downstreams
                for (int i = 1; i < Downstream.Length; i++)
                { 
                    result.Add(Downstream[i]);
                }

                return result;
            }
        }
    }
}
