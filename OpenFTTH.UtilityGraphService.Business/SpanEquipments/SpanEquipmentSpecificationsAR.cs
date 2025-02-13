using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using System;
using System.Collections.Generic;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments
{
    public class SpanEquipmentSpecifications : AggregateBase
    {
        public static readonly Guid UUID = Guid.Parse("989d9062-9149-46a4-b318-d5ba5d734ab9");

        private LookupCollection<SpanEquipmentSpecification> _spanEquipmentSpecifications = new LookupCollection<SpanEquipmentSpecification>();

        public SpanEquipmentSpecifications()
        {
            Id = UUID;
            Register<SpanEquipmentSpecificationAdded>(Apply);
            Register<SpanEquipmentSpecificationDeprecated>(Apply);
        }

        private void Apply(SpanEquipmentSpecificationDeprecated obj)
        {
            _spanEquipmentSpecifications[obj.SpanEquipmentSpecificationId] = _spanEquipmentSpecifications[obj.SpanEquipmentSpecificationId] with { Deprecated = true };
        }

        private void Apply(SpanEquipmentSpecificationAdded @event)
        {
            _spanEquipmentSpecifications.Add(@event.Specification);
        }

        public void AddSpecification(CommandContext cmdContext, SpanEquipmentSpecification spanEquipmentSpecification, LookupCollection<SpanStructureSpecification> spanStructureSpecifications, LookupCollection<Manufacturer> manufacturer)
        {
            if (_spanEquipmentSpecifications.ContainsKey(spanEquipmentSpecification.Id))
                throw new ArgumentException($"A span equipment specification with id: {spanEquipmentSpecification.Id} already exists");

            ValidateSpanStructureSpecificationReferences(spanEquipmentSpecification, spanStructureSpecifications);
            ValidateManufacturerReferences(spanEquipmentSpecification, manufacturer);
            ValidateSpanStructureLevelAndPosition(spanEquipmentSpecification, spanStructureSpecifications);

            RaiseEvent(
                new SpanEquipmentSpecificationAdded(spanEquipmentSpecification)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );
        }

        private static void ValidateSpanStructureSpecificationReferences(SpanEquipmentSpecification spanEquipmentSpecification, LookupCollection<SpanStructureSpecification> spanStructureSpecifications)
        {
            // Checkthat all span structures references exists
            foreach (var spanStructureTemplate in spanEquipmentSpecification.RootTemplate.GetAllSpanStructureTemplatesRecursive())
            {
                if (!spanStructureSpecifications.ContainsKey(spanStructureTemplate.SpanStructureSpecificationId))
                    throw new ArgumentException($"Cannot find span structure specification with id: {spanStructureTemplate.SpanStructureSpecificationId}");
            }
        }

        private static void ValidateManufacturerReferences(SpanEquipmentSpecification spanEquipmentSpecification, LookupCollection<Manufacturer> manufacturer)
        {
            // Check that all manufacturer exists if provided
            if (spanEquipmentSpecification.ManufacturerRefs != null)
            {
                foreach (var manufacturerId in spanEquipmentSpecification.ManufacturerRefs)
                {
                    if (!manufacturer.ContainsKey(manufacturerId))
                        throw new ArgumentException($"Cannot find manufaturer with id: {manufacturerId}");
                }
            }
        }

        private static void ValidateSpanStructureLevelAndPosition(SpanEquipmentSpecification spanEquipmentSpecification, LookupCollection<SpanStructureSpecification> spanStructureSpecifications)
        {
            // Used to check level+position uniqueness
            HashSet<(int, int)> levelPositionUsed = new HashSet<(int, int)>();

            // Root template must have level 1
            if (spanEquipmentSpecification.RootTemplate.Level != 1)
                throw new ArgumentException("Root template must always have level set to 1");

            levelPositionUsed.Add((spanEquipmentSpecification.RootTemplate.Level, spanEquipmentSpecification.RootTemplate.Position));

            var childsToCheck = spanEquipmentSpecification.RootTemplate.ChildTemplates;

            var expectedChildLevel = 2;

            while (childsToCheck.Length != 0)
            {
                List<SpanStructureTemplate> nextLevelChildsToCheck = new List<SpanStructureTemplate>();

                foreach (var childTemplate in childsToCheck)
                {
                    if (childTemplate.Level != expectedChildLevel)
                        throw new ArgumentException($"Expected level: {expectedChildLevel} in template referencing span structure specification: {childTemplate.SpanStructureSpecificationId}");

                    if (levelPositionUsed.Contains((childTemplate.Level, childTemplate.Position)))
                        throw new ArgumentException($"Level {childTemplate.Level} Position {childTemplate.Position} in template referencing span structure specification: {childTemplate.SpanStructureSpecificationId} is used more than once. Must be unique.");

                    levelPositionUsed.Add((childTemplate.Level, childTemplate.Position));

                    nextLevelChildsToCheck.AddRange(childTemplate.ChildTemplates);
                }

                childsToCheck = nextLevelChildsToCheck.ToArray();                

                expectedChildLevel++;
            }
        }

        public void DeprecatedSpecification(CommandContext cmdContext, Guid specificationId)
        {
            if (!_spanEquipmentSpecifications.ContainsKey(specificationId))
                throw new ArgumentException($"Cannot find span equipment specification with id: {specificationId}");

            RaiseEvent(
                new SpanEquipmentSpecificationDeprecated(specificationId)
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
