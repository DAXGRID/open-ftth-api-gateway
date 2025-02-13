using FluentResults;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.Interest.Events;
using OpenFTTH.RouteNetwork.Business.StateHandling.Interest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Business.Interest.Projections
{
    public class InterestsProjection : ProjectionBase
    {
        private readonly ConcurrentDictionary<Guid, RouteNetworkInterest> _interestById = new ConcurrentDictionary<Guid, RouteNetworkInterest>();
        private readonly InMemInterestRelationIndex _interestIndex = new InMemInterestRelationIndex();

        public InterestsProjection()
        {
            ProjectEventAsync<WalkOfInterestRegistered>(ProjectAsync);
            ProjectEventAsync<WalkOfInterestRouteNetworkElementsModified>(ProjectAsync);
            ProjectEventAsync<NodeOfInterestRegistered>(ProjectAsync);
            ProjectEventAsync<InterestUnregistered>(ProjectAsync);
        }

        public Result<RouteNetworkInterest> GetInterest(Guid interestId)
        {
            if (_interestById.TryGetValue(interestId, out RouteNetworkInterest? interest))
            {
                return Result.Ok<RouteNetworkInterest>(interest);
            }
            else
            {
                return Result.Fail<RouteNetworkInterest>($"No interest with id: {interestId} found");
            }
        }

        public Result<List<(RouteNetworkInterest, RouteNetworkInterestRelationKindEnum)>> GetInterestsByRouteNetworkElementId(Guid routeNetworkElementId)
        {
            var interestRelations = _interestIndex.GetRouteNetworkElementInterestRelations(routeNetworkElementId);

            List<(RouteNetworkInterest, RouteNetworkInterestRelationKindEnum)> result = new List<(RouteNetworkInterest, RouteNetworkInterestRelationKindEnum)>();

            foreach (var interestRelation in interestRelations)
            {
                result.Add((_interestById[interestRelation.Item1], interestRelation.Item2));
            }

            return Result.Ok<List<(RouteNetworkInterest, RouteNetworkInterestRelationKindEnum)>>(result);
        }


        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (WalkOfInterestRegistered @event):
                    _interestById.TryAdd(@event.Interest.Id, @event.Interest);
                    _interestIndex.Add(@event.Interest);
                    break;

                case (NodeOfInterestRegistered @event):
                    _interestById[@event.Interest.Id] = @event.Interest;
                    _interestIndex.Add(@event.Interest);
                    break;

                case (WalkOfInterestRouteNetworkElementsModified @event):
                    if (_interestById.TryGetValue(@event.InterestId, out var existingInterestToModify))
                    {
                        var updatedInterest = existingInterestToModify with { RouteNetworkElementRefs = @event.RouteNetworkElementIds };
                        _interestById.TryUpdate(@event.InterestId, updatedInterest, existingInterestToModify);
                        _interestIndex.Update(updatedInterest, existingInterestToModify);
                    }
                    break;

                case (InterestUnregistered @event):
                    if (_interestById.TryGetValue(@event.InterestId, out var existingInterestToUnregister))
                    {
                        _interestById.TryRemove(@event.InterestId, out _);
                        _interestIndex.Remove(existingInterestToUnregister);
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
