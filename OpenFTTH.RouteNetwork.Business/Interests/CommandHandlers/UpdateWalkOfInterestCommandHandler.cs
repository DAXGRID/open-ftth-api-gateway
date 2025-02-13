using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.Interest.Projections;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Business.MutationHandling
{
    public class UpdateWalkOfInterestCommandHandler : ICommandHandler<UpdateWalkOfInterest, Result<RouteNetworkInterest>>
    {
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public UpdateWalkOfInterestCommandHandler(IEventStore eventStore, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _routeNetworkRepository = routeNodeRepository;
        }

        public Task<Result<RouteNetworkInterest>> HandleAsync(UpdateWalkOfInterest command)
        {
            var interestAR = _eventStore.Aggregates.Load<InterestAR>(command.InterestId);

            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            var walkOfInterest = new RouteNetworkInterest(command.InterestId, RouteNetworkInterestKindEnum.WalkOfInterest, command.WalkIds);

            var walkValidator = new WalkValidator(_routeNetworkRepository);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var updateInterestResult = interestAR.UpdateRouteNetworkElements(commandContext, walkOfInterest, interestProjection, walkValidator);

            if (updateInterestResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(interestAR);
            }

            return Task.FromResult(updateInterestResult);
        }
    }
}
