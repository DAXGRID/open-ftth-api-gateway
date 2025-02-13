using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.TerminalEquipments
{
    public class TerminalStructureSpecifications : AggregateBase
    {
        public static readonly Guid UUID = Guid.Parse("cc2b3c32-1704-4b8b-babf-a15950c1b317");

        private LookupCollection<TerminalStructureSpecification> _terminalStructureSpecifications = new();

        public TerminalStructureSpecifications()
        {
            Id = UUID;
            Register<TerminalStructureSpecificationAdded>(Apply);
        }

        private void Apply(TerminalStructureSpecificationAdded @event)
        {
            _terminalStructureSpecifications.Add(@event.Specification);
        }

        public void AddSpecification(CommandContext cmdContext, TerminalStructureSpecification terminalStructureSpecification, LookupCollection<Manufacturer> manufacturer)
        {
            if (_terminalStructureSpecifications.ContainsKey(terminalStructureSpecification.Id))
                throw new ArgumentException($"A terminal structure specification with id: {terminalStructureSpecification.Id} already exists");

            ValidateSpecificationName(terminalStructureSpecification);
            ValidateManufacturerReferences(terminalStructureSpecification, manufacturer);
            ValidateTerminalNameUniqueness(terminalStructureSpecification);

            RaiseEvent(
                new TerminalStructureSpecificationAdded(terminalStructureSpecification)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );
        }

        private void ValidateSpecificationName(TerminalStructureSpecification terminalStructureSpecification)
        {
            if (String.IsNullOrEmpty(terminalStructureSpecification.Name))
                throw new ArgumentException($"Specification name is mandatory. Error adding terminal structure specification with id: {terminalStructureSpecification.Id}");

            if (_terminalStructureSpecifications.Any(t => t.Name.ToLower() == terminalStructureSpecification.Name.ToLower()))
                throw new ArgumentException($"Specification name: '{terminalStructureSpecification.Name}' is already used. Must be unique. Error adding template structure specification with id: {terminalStructureSpecification.Id}");
        }

        private static void ValidateManufacturerReferences(TerminalStructureSpecification terminalStructureSpecification, LookupCollection<Manufacturer> manufacturer)
        {
            if (terminalStructureSpecification.ManufacturerRefs != null)
            {
                foreach (var manufacturerId in terminalStructureSpecification.ManufacturerRefs)
                {
                    if (!manufacturer.ContainsKey(manufacturerId))
                        throw new ArgumentException($"Cannot find manufacturer with id: {manufacturerId}");
                }
            }
        }

        private static void ValidateTerminalNameUniqueness(TerminalStructureSpecification terminalStructureSpecification)
        {
            HashSet<string> terminalNameUsed = new();

            foreach (var terminalTemplate in terminalStructureSpecification.TerminalTemplates)
            {
                var key = terminalTemplate.Name.ToLower() + ":" + terminalTemplate.Direction.ToString();

                if (terminalNameUsed.Contains(key))
                    throw new ArgumentException($"Terminal name: {terminalTemplate.Name} specified in terminal structure specification: {terminalStructureSpecification.Name} is used more than once. Must be unique.");

                terminalNameUsed.Add(key);
            }
        }
    }
}
