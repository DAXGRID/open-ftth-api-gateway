using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.Graph
{
    public interface IUtilityGraphSegmentRef : IUtilityGraphElement
    {
        public Guid SpanEquipmentId { get; }
        SpanEquipment SpanEquipment(UtilityNetworkProjection utilityNetwork);
        SpanSegment SpanSegment(UtilityNetworkProjection utilityNetwork);
        ushort StructureIndex { get; }
        ushort SegmentIndex { get; }
        IUtilityGraphSegmentRef CreateWithNewSegmentIndex(ushort segmentIndex);
    }
}
