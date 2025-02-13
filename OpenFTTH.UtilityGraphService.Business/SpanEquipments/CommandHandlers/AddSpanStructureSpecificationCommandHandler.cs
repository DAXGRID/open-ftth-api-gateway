using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using System;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class AddSpanStructureSpecificationCommandHandler : ICommandHandler<AddSpanStructureSpecification, Result>
    {
        private readonly IEventStore _eventStore;

        public AddSpanStructureSpecificationCommandHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(AddSpanStructureSpecification command)
        {
            var aggreate = _eventStore.Aggregates.Load<SpanStructureSpecificationsAR>(SpanStructureSpecificationsAR.UUID);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            try
            {
                aggreate.AddSpecification(commandContext, command.Specification);
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(Result.Fail(ex.Message));
            }

            _eventStore.Aggregates.Store(aggreate);

            return Task.FromResult(Result.Ok());
        }
    }
}

  