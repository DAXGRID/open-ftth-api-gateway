using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Represents a line for display in a span equipment connectivity view
    /// </summary>
    public record SpanEquipmentPassageViewLineInfo
    {
        public Guid SpanSegmentId { get; }
        public Guid FromNodeId { get; set; }
        public Guid ToNodeId { get; set;  }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? ConduitId { get; init; }
        public string? OuterConduitInfo { get; init; }
        public string? InnerConduitInfo { get; init; }
        public Guid[] RouteSegmentIds { get; init; }
        public string[] RouteSegmentGeometries { get; init; }

        public double SegmentLength { get; init; }
        public double CumulativeDistance { get; set; }

        public SpanEquipmentPassageViewLineInfo(Guid spanSegmentId)
        {
            SpanSegmentId = spanSegmentId;

            // TODO: Implementation missing
            RouteSegmentIds = new Guid[0];
            RouteSegmentGeometries = new string[0];
        }

        public void Reverse(double newTotalLength)
        {
            CumulativeDistance = newTotalLength;

            var oldFromNode = FromNodeId;
            FromNodeId = ToNodeId;
            ToNodeId = oldFromNode;

            var oldFrom = From;
            From = To;
            To = oldFrom;
        }
    }
}
