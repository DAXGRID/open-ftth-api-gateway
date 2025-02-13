using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    public record TerminalEquipmentAZConnectivityViewEndInfo
    {
        public TerminalEquipmentAZConnectivityViewTerminalInfo Terminal { get; init; }
        public FaceKindEnum FaceKind { get; init; }
        public Guid ConnectedToSpanSegmentId { get; init; }
        public string? ConnectedTo { get; init; }
        public string? End { get; init; }

        public TerminalEquipmentAZConnectivityViewEndInfo(TerminalEquipmentAZConnectivityViewTerminalInfo terminal, FaceKindEnum faceKind)
        {
            Terminal = terminal;
            FaceKind = faceKind;
        }
    }
}
