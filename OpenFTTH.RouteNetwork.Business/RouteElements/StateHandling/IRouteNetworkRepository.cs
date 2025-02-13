using FluentResults;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using System.Collections.Generic;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling
{
    public interface IRouteNetworkRepository
    {
        Result<List<IRouteNetworkElement>> GetRouteElements(RouteNetworkElementIdList guids, bool createSubstitudesForMissingRouteNetworkElements = false);

        IRouteNetworkState NetworkState { get; }

        IEnumerable<IRouteNetworkElement> GetByEnvelope(long version, Envelope extent);
    }
}
