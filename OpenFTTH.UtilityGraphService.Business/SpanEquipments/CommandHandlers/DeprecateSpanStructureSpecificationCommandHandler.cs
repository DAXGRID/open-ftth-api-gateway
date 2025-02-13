using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class DeprecateSpanStructureSpecificationCommandHandler : ICommandHandler<DeprecateSpanStructureSpecification, Result>
    {
        private readonly IEventStore _eventStore;

        public DeprecateSpanStructureSpecificationCommandHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(DeprecateSpanStructureSpecification command)
        {
            var aggreate = _eventStore.Aggregates.Load<SpanStructureSpecificationsAR>(SpanStructureSpecificationsAR.UUID);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            aggreate.DeprecatedSpecification(commandContext, command.SpanStructureSpecificationId);

            _eventStore.Aggregates.Store(aggreate);

            return Task.FromResult(Result.Ok());
        }
    }
}

  