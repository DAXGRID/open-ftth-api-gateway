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
    public class RegisterWalkOfInterestCommandHandler : ICommandHandler<RegisterWalkOfInterest, Result<RouteNetworkInterest>>
    {
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public RegisterWalkOfInterestCommandHandler(
            IEventStore eventStore,
            IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _routeNetworkRepository = routeNodeRepository;
            eventStore.ScanForProjections();
        }

        public Task<Result<RouteNetworkInterest>> HandleAsync(RegisterWalkOfInterest command)
        {
            var interestProjection = _eventStore.Projections.Get<InterestsProjection>();

            var walkOfInterest = new RouteNetworkInterest(command.InterestId, RouteNetworkInterestKindEnum.WalkOfInterest, command.WalkIds);

            var walkValidator = new WalkValidator(_routeNetworkRepository);

            var interestAR = new InterestAR();

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            var registerInterestResult = interestAR.RegisterWalkOfInterest(commandContext, walkOfInterest, interestProjection, walkValidator);

            if (registerInterestResult.IsSuccess)
            {
                _eventStore.Aggregates.Store(interestAR);
            }

            return Task.FromResult(registerInterestResult);
        }
    }
}
