using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments
{
    public class NodeContainerSpecificationsAR : AggregateBase
    {
        public static readonly Guid UUID = Guid.Parse("f9ae84a4-87c9-415d-ba78-ae7cbd45d23d");

        private LookupCollection<NodeContainerSpecification> _nodeContainerSpecifications = new LookupCollection<NodeContainerSpecification>();

        public NodeContainerSpecificationsAR()
        {
            Id = UUID;
            Register<NodeContainerSpecificationAdded>(Apply);
        }

        private void Apply(NodeContainerSpecificationAdded @event)
        {
            _nodeContainerSpecifications.Add(@event.Specification);
        }

        public void AddSpecification(CommandContext cmdContext, NodeContainerSpecification nodeContainerSpecifiation, LookupCollection<Manufacturer> manufacturer)
        {
            if (_nodeContainerSpecifications.ContainsKey(nodeContainerSpecifiation.Id))
                throw new ArgumentException($"A node container specification with id: {nodeContainerSpecifiation.Id} already exists");

            RaiseEvent(
                new NodeContainerSpecificationAdded(nodeContainerSpecifiation)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );
        }
    }
}
