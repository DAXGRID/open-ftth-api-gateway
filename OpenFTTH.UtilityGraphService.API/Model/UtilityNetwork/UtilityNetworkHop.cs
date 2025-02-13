using System;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork
{
    public record UtilityNetworkHop
    {
        public Guid FromNodeId { get; }
        public Guid ToNodeId { get; }
        public SpanEquipmentSpanEquipmentAffix[] ParentAffixes { get; }

        public UtilityNetworkHop(Guid fromNodeId, Guid toNodeId, SpanEquipmentSpanEquipmentAffix[] parentAffixes)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            ParentAffixes = parentAffixes;

            if (parentAffixes.Length == 0)
                throw new ApplicationException("A utility hop must always have at least one parent affix");
        }

        public UtilityNetworkHop Reverse()
        {
            return new UtilityNetworkHop(ToNodeId, FromNodeId, ParentAffixes.Reverse().ToArray());
        }
    }
}
