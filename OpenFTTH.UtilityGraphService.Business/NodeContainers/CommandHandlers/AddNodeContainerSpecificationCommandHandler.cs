using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Projections;
using System;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.CommandHandlers
{
    public class AddNodeContainerSpecificationCommandHandler : ICommandHandler<AddNodeContainerSpecification, Result>
    {
        private readonly IEventStore _eventStore;

        public AddNodeContainerSpecificationCommandHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<Result> HandleAsync(AddNodeContainerSpecification command)
        {
            var aggreate = _eventStore.Aggregates.Load<NodeContainerSpecificationsAR>(NodeContainerSpecificationsAR.UUID);

            var manufacturer = _eventStore.Projections.Get<ManufacturerProjection>().Manufacturer;

            var commandContext = new CommandContext(command.CorrelationId, command.CmdId, command.UserContext);

            try
            {
                aggreate.AddSpecification(commandContext, command.Specification, manufacturer);
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

  