using OpenFTTH.Results;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class AddSpanEquipmentSpecificationCommandHandler : ICommandHandler<AddSpanEquipmentSpecification, Result>
    {
        private readonly IEventStore _eventStore;

        public AddSpanEquipmentSpecificationCommandHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(AddSpanEquipmentSpecification command)
        {
            var aggreate = _eventStore.Aggregates.Load<SpanEquipmentSpecifications>(SpanEquipmentSpecifications.UUID);

            var spanStructureSpecifications = _eventStore.Projections.Get<SpanStructureSpecificationsProjection>().Specifications;

            var manufacturer = _eventStore.Projections.Get<ManufacturerProjection>().Manufacturer;

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            try
            {
                aggreate.AddSpecification(commandContext, command.Specification, spanStructureSpecifications, manufacturer);
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(Result.Fail(new SpecificationError(SpecificationErrorCodes.SPECIFICATION_IS_INVALID, ex.Message)));
            }

            _eventStore.Aggregates.Store(aggreate);

            return Task.FromResult(Result.Ok());
        }
    }
}
