using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using System;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers.CommandHandlers
{
    public class AddRackSpecificationCommandHandler : ICommandHandler<AddRackSpecification, Result>
    {
        private readonly IEventStore _eventStore;

        public AddRackSpecificationCommandHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(AddRackSpecification command)
        {
            var aggreate = _eventStore.Aggregates.Load<RackSpecificationsAR>(RackSpecificationsAR.UUID);

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            try
            {
                aggreate.AddSpecification(commandContext, command.Specification);
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

  