using System;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views
{
    public record ConnectivityFaceConnection
    {
        public Guid TerminalOrSegmentId { get; set; }
        public string Name { get; set; }
        public string EndInfo { get; set; }
        public bool IsConnected { get; set; }
    }
}
