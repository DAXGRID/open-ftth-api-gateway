using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.Interest;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.RouteNetwork.Business.MutationHandling
{
    public class ValidateWalkOfInterestCommandHandler : ICommandHandler<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>
    {
        private readonly IEventStore _eventStore;
        private readonly IRouteNetworkRepository _routeNetworkRepository;

        public ValidateWalkOfInterestCommandHandler(IEventStore eventStore, IRouteNetworkRepository routeNodeRepository)
        {
            _eventStore = eventStore;
            _routeNetworkRepository = routeNodeRepository;
        }

        public Task<Result<ValidatedRouteNetworkWalk>> HandleAsync(ValidateWalkOfInterest command)
        {
            var walkValidator = new WalkValidator(_routeNetworkRepository);

            var validateResult = walkValidator.ValidateWalk(command.WalkIds);

            if (validateResult.IsFailed)
                return Task.FromResult(Result.Fail<ValidatedRouteNetworkWalk>(validateResult.Errors.First()));
            else
            {
                return Task.FromResult(Result.Ok<ValidatedRouteNetworkWalk>(new ValidatedRouteNetworkWalk(validateResult.Value)));
            }
        }
    }
}
