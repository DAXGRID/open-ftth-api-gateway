using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    public record RouteNetworkElementFilterOptions
    {
        public bool IncludeRouteNodeInfo { get; init; }
        public bool IncludeRouteSegmentInfo { get; init; }

        public bool IncludeCoordinates { get; init; }
        public bool IncludeNamingInfo { get; init; }
        public bool IncludeMappingInfo { get; init; }
        public bool IncludeLifecycleInfo { get; init; }
        public bool IncludeSafetyInfo { get; init; }

        public RouteNetworkElementFilterOptions()
        {
        }
    }
}
