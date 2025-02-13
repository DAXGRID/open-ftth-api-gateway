using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments
{
    public class TerminalEquipmentSpecifications : AggregateBase
    {
        public static readonly Guid UUID = Guid.Parse("9873d63e-1dc5-4ca4-be44-a3a2099d39e8");

        private LookupCollection<TerminalEquipmentSpecification> _terminalEquipmentSpecifications = new();

        public TerminalEquipmentSpecifications()
        {
            Id = UUID;
            Register<TerminalEquipmentSpecificationAdded>(Apply);
        }

        private void Apply(TerminalEquipmentSpecificationAdded @event)
        {
            _terminalEquipmentSpecifications.Add(@event.Specification);
        }

        public void AddSpecification(CommandContext cmdContext, TerminalEquipmentSpecification terminalEquipmentSpecification, LookupCollection<TerminalStructureSpecification> terminalStructureSpecifications, LookupCollection<Manufacturer> manufacturer)
        {
            if (_terminalEquipmentSpecifications.ContainsKey(terminalEquipmentSpecification.Id))
                throw new ArgumentException($"A terminal equipment specification with id: {terminalEquipmentSpecification.Id} already exists");

            ValidateSpecificationName(terminalEquipmentSpecification);
            ValidateTerminalStructureSpecificationReferences(terminalEquipmentSpecification, terminalStructureSpecifications);
            ValidateManufacturerReferences(terminalEquipmentSpecification, manufacturer);
            ValidateTerminalStructureTemplatePositionUniqueness(terminalEquipmentSpecification, terminalStructureSpecifications);
            ValidateRackEquipment(terminalEquipmentSpecification);

            RaiseEvent(
                new TerminalEquipmentSpecificationAdded(terminalEquipmentSpecification)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );
        }

        private void ValidateSpecificationName(TerminalEquipmentSpecification terminalEquipmentSpecification)
        {
            if (String.IsNullOrEmpty(terminalEquipmentSpecification.Name))
                throw new ArgumentException($"Specification name is mandatory. Error adding terminal equipment specification with id: {terminalEquipmentSpecification.Id}");

            if (_terminalEquipmentSpecifications.Any(t => t.Name.ToLower() == terminalEquipmentSpecification.Name.ToLower()))
                throw new ArgumentException($"Specification name: '{terminalEquipmentSpecification.Name}' is already used. Must be unique. Error adding template equipment specification with id: {terminalEquipmentSpecification.Id}");
        }

        private static void ValidateTerminalStructureSpecificationReferences(TerminalEquipmentSpecification equipmentEquipmentSpecification, LookupCollection<TerminalStructureSpecification> terminalStructureSpecifications)
        {
            foreach (var terminalStructureTemplate in equipmentEquipmentSpecification.StructureTemplates)
            {
                if (!terminalStructureSpecifications.ContainsKey(terminalStructureTemplate.TerminalStructureSpecificationId))
                    throw new ArgumentException($"Cannot find terminal structure specification with id: {terminalStructureTemplate.TerminalStructureSpecificationId}");
            }
        }

        private static void ValidateManufacturerReferences(TerminalEquipmentSpecification equipmentEquipmentSpecification, LookupCollection<Manufacturer> manufacturer)
        {
            if (equipmentEquipmentSpecification.ManufacturerRefs != null)
            {
                foreach (var manufacturerId in equipmentEquipmentSpecification.ManufacturerRefs)
                {
                    if (!manufacturer.ContainsKey(manufacturerId))
                        throw new ArgumentException($"Cannot find manufacturer with id: {manufacturerId}");
                }
            }
        }

        private static void ValidateTerminalStructureTemplatePositionUniqueness(TerminalEquipmentSpecification terminalEquipmentSpecification, LookupCollection<TerminalStructureSpecification> terminalStructureSpecifications)
        {
            HashSet<int> positionUsed = new();

            foreach (var structureTemplate in terminalEquipmentSpecification.StructureTemplates)
            {
                if (positionUsed.Contains(structureTemplate.Position))
                    throw new ArgumentException($"Structure position: {structureTemplate.Position} specified in terminal equipment specification: {terminalEquipmentSpecification.Name} is used more than once. Must be unique.");

                positionUsed.Add(structureTemplate.Position);
            }
        }

        private static void ValidateRackEquipment(TerminalEquipmentSpecification terminalEquipmentSpecification)
        {
            if (terminalEquipmentSpecification.IsRackEquipment && terminalEquipmentSpecification.HeightInRackUnits < 1)
            {
                throw new ArgumentException($"Expected a positive height (in rack unit) on a rack equipment. Error validating terminal equipment specification: {terminalEquipmentSpecification.Name}");
            }
        }
    }
}
