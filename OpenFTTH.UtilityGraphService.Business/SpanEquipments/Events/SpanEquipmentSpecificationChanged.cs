using OpenFTTH.Events;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events
{
    public record SpanEquipmentSpecificationChanged : EventStoreBaseEvent
    {
        public Guid SpanEquipmentId { get; }

        public Guid NewSpecificationId { get; }

        public StructureModificationInstruction[] StructureModificationInstructions { get; }

        public SpanEquipmentSpecificationChanged(Guid spanEquipmentId, Guid newSpecificationId, StructureModificationInstruction[] structureModificationInstructions)
        {
            SpanEquipmentId = spanEquipmentId;
            NewSpecificationId = newSpecificationId;
            StructureModificationInstructions = structureModificationInstructions;
        }
    }

    public record StructureModificationInstruction
    {
        public Guid StructureId { get; }
        public bool StructureToBeDeleted { get; }
        public Guid? StructureSpecificationIdToBeUpdated { get; init; }
        public SpanStructure? NewStructureToBeInserted { get; init; }

        public StructureModificationInstruction(Guid structureId, bool structureToBeDeleted)
        {
            StructureId = structureId;
            StructureToBeDeleted = structureToBeDeleted;
        }
    }
}
