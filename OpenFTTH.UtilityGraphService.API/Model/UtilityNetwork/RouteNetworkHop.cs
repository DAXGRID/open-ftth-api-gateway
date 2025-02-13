namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record RouteNetworkHop
    {
        public short HopNumber { get; }
        public short FromNodeIndex { get; }
        public short ToNodeIndex { get; }

        public RouteNetworkHop(short hopNumber, short fromNodeIndex, short toNodeIndex)
        {
            HopNumber = hopNumber;
            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
        }
    }
}
