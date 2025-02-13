using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    /// <summary>
    /// Represents a line for display in a span equipment connectivity view
    /// </summary>
    public record SpanEquipmentAZConnectivityViewLineInfo
    {
        public int SequenceNumber { get; }
        public string Name { get; }
        public Guid SpanSegmentId { get; }

        public SpanEquipmentAZConnectivityViewEndInfo? A { get; init; }
        public SpanEquipmentAZConnectivityViewEndInfo? Z { get; init; }

        public SpanEquipmentAZConnectivityViewLineInfo(int sequenceNumber, string name, Guid spanSegmentId)
        {
            SequenceNumber = sequenceNumber;
            Name = name;
            SpanSegmentId = spanSegmentId;
        }
    }
}
