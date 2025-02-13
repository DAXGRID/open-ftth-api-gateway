using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using System.Runtime.Serialization;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    /// <summary>
    /// Used to represent span equipment information related to a specific route network element as part 
    /// of the GetEquipmentRelatedToRouteElementQueryResult data structure.
    /// </summary>
    public record RelatedSpanEquipment : SpanEquipment
    {
        private readonly RelatedSpanStructure _rootStructure;

        internal SpanEquipmentSpecification? _specification;

        public RelatedSpanEquipment(Guid id, Guid specificationId, RelatedSpanStructure rootStructure, Guid walkOfInterestId, Guid[] nodesOfInterestIds, SpanStructure[] spanStructures) : base(id, specificationId, walkOfInterestId, nodesOfInterestIds, spanStructures)
        {
            this._rootStructure = rootStructure;
        }
    }
}
