using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using System;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.NodeContainers
{
    public class RackSpecificationsAR : AggregateBase
    {
        public static readonly Guid UUID = Guid.Parse("4e1a0af2-5ce0-4035-9396-575279ba48bd");

        private readonly LookupCollection<RackSpecification> _rackSpecifications = new();

        public RackSpecificationsAR()
        {
            Id = UUID;
            Register<RackSpecificationAdded>(Apply);
        }

        private void Apply(RackSpecificationAdded @event)
        {
            _rackSpecifications.Add(@event.Specification);
        }

        public void AddSpecification(CommandContext cmdContext, RackSpecification rackSpecification)
        {
            if (_rackSpecifications.ContainsKey(rackSpecification.Id))
                throw new ArgumentException($"A rack specification with id: {rackSpecification.Id} already exists");

            ValidateSpecificationName(rackSpecification);

            RaiseEvent(
                new RackSpecificationAdded(rackSpecification)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );
        }
        private void ValidateSpecificationName(RackSpecification rackSpecification)
        {
            if (String.IsNullOrEmpty(rackSpecification.Name))
                throw new ArgumentException($"Rack specification name is mandatory. Error adding rack specification with id: {rackSpecification.Id}");

            if (_rackSpecifications.Any(t => t.Name.ToLower() == rackSpecification.Name.ToLower()))
                throw new ArgumentException($"Rack specification name: '{rackSpecification.Name}' is already used. Must be unique. Error adding rack specification with id: {rackSpecification.Id}");
        }
    }
}
