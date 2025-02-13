using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Queries;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.QueryHandling
{
    public class GetTerminalEquipmentSpecificationsQueryHandler
        : IQueryHandler<GetTerminalEquipmentSpecifications, Result<LookupCollection<TerminalEquipmentSpecification>>>
    {
        private readonly IEventStore _eventStore;        

        public GetTerminalEquipmentSpecificationsQueryHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result<LookupCollection<TerminalEquipmentSpecification>>> HandleAsync(GetTerminalEquipmentSpecifications query)
        {
            var specifications = _eventStore.Projections.Get<TerminalEquipmentSpecificationsProjection>();

            return Task.FromResult(
                Result.Ok<LookupCollection<TerminalEquipmentSpecification>>(
                    specifications.Specifications
                )
            );
        }
    }
}
