using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.RouteNetwork.API.Model
{
    public class ValidatedRouteNetworkWalk
    {
        public RouteNetworkElementIdList RouteNetworkElementRefs { get; }

        public ValidatedRouteNetworkWalk(RouteNetworkElementIdList routeNetworkElementRefs)
        {
            RouteNetworkElementRefs = routeNetworkElementRefs;
        }

        public List<Guid> SegmentIds
        {
            get
            {
                List<Guid> result = new();

                for (int i = 1; i < RouteNetworkElementRefs.Count; i += 2)
                {
                    result.Add(RouteNetworkElementRefs[i]);
                }

                return result;
            }
        }

        public List<Guid> NodeIds
        {
            get
            {
                List<Guid> result = new();

                for (int i = 0; i < RouteNetworkElementRefs.Count; i += 2)
                {
                    result.Add(RouteNetworkElementRefs[i]);
                }

                return result;
            }
        }

        public Guid FromNodeId
        {
            get
            {
                return (RouteNetworkElementRefs.First());
            }
        }

        public Guid ToNodeId
        {
            get
            {
                return (RouteNetworkElementRefs.Last());
            }
        }

        public ValidatedRouteNetworkWalk Reverse()
        {
            var newRouteNetworkElementRefs = new RouteNetworkElementIdList();

            for (int i = 0; i < this.RouteNetworkElementRefs.Count; i++)
            {
                newRouteNetworkElementRefs.Add(RouteNetworkElementRefs[RouteNetworkElementRefs.Count - (i + 1)]);
            }

            return new ValidatedRouteNetworkWalk(newRouteNetworkElementRefs);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null)
                return false;

            var walk = (ValidatedRouteNetworkWalk)obj;

            if (walk == null)
                return false;

            if (walk.RouteNetworkElementRefs.Count != this.RouteNetworkElementRefs.Count)
                return false;

            // Compare forward
            bool refsEqual = true;

            for (int i = 0; i < this.RouteNetworkElementRefs.Count; i++)
            {
                if (walk.RouteNetworkElementRefs[i] != this.RouteNetworkElementRefs[i])
                    refsEqual = false;
            }

            if (refsEqual)
                return true;

            // Try if sequence if route element ids are the same if we compare them backwards
            bool refsBackwardsEqual = true;

            for (int i = 0; i < this.RouteNetworkElementRefs.Count; i++)
            {
                if (walk.RouteNetworkElementRefs[walk.RouteNetworkElementRefs.Count - (i + 1)] != this.RouteNetworkElementRefs[i])
                    refsBackwardsEqual = false;
            }

            if (refsBackwardsEqual)
                return true;

            return false;
        }

    }
}
