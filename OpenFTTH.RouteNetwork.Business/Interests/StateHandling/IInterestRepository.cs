using FluentResults;
using OpenFTTH.RouteNetwork.API.Model;
using System;
using System.Collections.Generic;

namespace OpenFTTH.RouteNetwork.Business.StateHandling
{
    public interface IInterestRepository
    {
        RouteNetworkInterest RegisterWalkOfInterest(Guid interestId, RouteNetworkElementIdList walkIds);
        Result<RouteNetworkInterest> GetInterest(Guid interestId);
        Result<List<(RouteNetworkInterest, RouteNetworkInterestRelationKindEnum)>> GetInterestsByRouteNetworkElementId(Guid routeNetworkElementId);
    }
}
