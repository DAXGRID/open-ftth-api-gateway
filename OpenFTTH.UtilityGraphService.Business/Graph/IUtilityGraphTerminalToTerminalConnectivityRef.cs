using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public interface IUtilityGraphTerminalToTerminalConnectivityRef : IUtilityGraphElement
    {
        Guid RouteNodeId { get; }
        double FiberCoordLength { get; }
     }
}
