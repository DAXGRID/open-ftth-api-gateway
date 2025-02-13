using OpenFTTH.RouteNetwork.API.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.RouteNetwork.Business.StateHandling.Interest
{
    /// <summary>
    /// In-memory index holding relations from route network elements to the interests that starts, ends or pass through them.
    /// Used for fast lookup of all interests related to a given route network element.
    /// </summary>
    public class InMemInterestRelationIndex
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, (Guid, RouteNetworkInterestRelationKindEnum)>> _routeElementInterestRelations = new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, (Guid, RouteNetworkInterestRelationKindEnum)>>();

        /// <summary>
        /// Update index of existing interest
        /// </summary>
        /// <param name="interest"></param>
        public void Update(RouteNetworkInterest updatedInterest, RouteNetworkInterest existingInterest)
        {
            // Remove all interest relations from index
            foreach (var routeElementId in existingInterest.RouteNetworkElementRefs)
            {
                if (_routeElementInterestRelations.ContainsKey(routeElementId))
                    _routeElementInterestRelations[routeElementId].TryRemove(existingInterest.Id, out var _);
            }

            Add(updatedInterest);
        }

        /// <summary>
        /// Add interest to index. If already indexed, eventually old route element references will *not* be removed.
        /// </summary>
        /// <param name="interest"></param>
        public void Add(RouteNetworkInterest interest)
        {
            // Create index entries for all route elements ids covered by the interest
            for (int i = 0; i < interest.RouteNetworkElementRefs.Count; i++)
            {
                var currentRouteElementId = interest.RouteNetworkElementRefs[i];

                RouteNetworkInterestRelationKindEnum relKind = RouteNetworkInterestRelationKindEnum.Start;

                if (interest.RouteNetworkElementRefs.Count == 1)
                    relKind = RouteNetworkInterestRelationKindEnum.InsideNode;
                else if (i == 0)
                    relKind = RouteNetworkInterestRelationKindEnum.Start;
                else if (i == interest.RouteNetworkElementRefs.Count - 1)
                    relKind = RouteNetworkInterestRelationKindEnum.End;
                else
                    relKind = RouteNetworkInterestRelationKindEnum.PassThrough;

                var interestRelations = _routeElementInterestRelations.GetOrAdd(currentRouteElementId, new ConcurrentDictionary<Guid, (Guid, RouteNetworkInterestRelationKindEnum)>());

                interestRelations.TryAdd(interest.Id, (interest.Id, relKind));
            }
        }

        public void Remove(RouteNetworkInterest interestToRemove)
        {
            // Remove relations that don't exist anymore in updated interest
            foreach (var routeElementId in interestToRemove.RouteNetworkElementRefs)
            {
                if (_routeElementInterestRelations.ContainsKey(routeElementId))
                    _routeElementInterestRelations[routeElementId].TryRemove(interestToRemove.Id, out var _);
            }
        }

        public List<(Guid, RouteNetworkInterestRelationKindEnum)> GetRouteNetworkElementInterestRelations(Guid routeElementId)
        {
            if (_routeElementInterestRelations.TryGetValue(routeElementId, out var interestRelationList))
            {
                return interestRelationList.Values.ToList();
            }
            else
            {
                return new List<(Guid, RouteNetworkInterestRelationKindEnum)>();
            }
        }

        private void RemoveExistingInterestIdsFromIndex(Guid interestId)
        {
            // We iterate through every route element to interest relation in the index using old fashioned foreach loops to be most CPU efficient
            // One could avoid this to by having som additional index from interest to route elements, but that would require more memory
            foreach (var interestRelationList in _routeElementInterestRelations)
            {
                bool routeElementContainsInterestRelation = false;

                foreach (var interestRelation in interestRelationList.Value)
                {
                    if (interestRelation.Value.Item1 == interestId)
                    {
                        routeElementContainsInterestRelation = true;
                    }
                }

                if (routeElementContainsInterestRelation)
                {
                    _routeElementInterestRelations[interestRelationList.Key].TryRemove(interestId, out var _);
                }
            }
        }
    }
}
