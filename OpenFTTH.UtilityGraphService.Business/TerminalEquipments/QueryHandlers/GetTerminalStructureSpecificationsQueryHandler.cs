using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalStructures.QueryHandling
{
    public class GetTerminalStructureSpecificationsQueryHandler
        : IQueryHandler<GetTerminalStructureSpecifications, Result<LookupCollection<TerminalStructureSpecification>>>
    {           
        private readonly IEventStore _eventStore;        

        public GetTerminalStructureSpecificationsQueryHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result<LookupCollection<TerminalStructureSpecification>>> HandleAsync(GetTerminalStructureSpecifications query)
        {
            var specifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>();

            return Task.FromResult(
                Result.Ok<LookupCollection<TerminalStructureSpecification>>(
                    specifications.Specifications
                )
            );
        }
    }
}
