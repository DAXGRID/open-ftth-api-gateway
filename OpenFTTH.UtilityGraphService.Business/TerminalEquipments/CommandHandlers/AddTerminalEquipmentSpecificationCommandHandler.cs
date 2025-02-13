using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Projections;
using System;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments.CommandHandlers
{
    public class AddTerminalEquipmentSpecificationCommandHandler : ICommandHandler<AddTerminalEquipmentSpecification, Result>
    {
        private readonly IEventStore _eventStore;

        public AddTerminalEquipmentSpecificationCommandHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(AddTerminalEquipmentSpecification command)
        {
            var aggreate = _eventStore.Aggregates.Load<TerminalEquipmentSpecifications>(TerminalEquipmentSpecifications.UUID);

            var terminalStructureSpecifications = _eventStore.Projections.Get<TerminalStructureSpecificationsProjection>().Specifications;

            var manufacturer = _eventStore.Projections.Get<ManufacturerProjection>().Manufacturer;

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            try
            {
                aggreate.AddSpecification(commandContext, command.Specification, terminalStructureSpecifications, manufacturer);
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

  