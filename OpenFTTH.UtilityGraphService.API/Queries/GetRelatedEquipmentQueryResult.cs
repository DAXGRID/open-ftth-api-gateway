using OpenFTTH.UtilityGraphService.API.Model.RouteNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    /// <summary>
    /// Query result object used to hold equipment information related to a specific route network element (a route node or route segment)
    /// </summary>
    public class GetRelatedEquipmentQueryResult
    {
        private readonly RouteNetworkElementInfo _queriedRouteNetworkElement;

        private SpanEquipmentSpecification[]? _spanEquipmentSpecifications;

        private RelatedSpanEquipment[]? _relatedSpanEquipment;

        public GetRelatedEquipmentQueryResult(RouteNetworkElementInfo queriedRouteNetworkElement)
        {
            _queriedRouteNetworkElement = queriedRouteNetworkElement;
        }

        public RouteNetworkElementInfo QueriedRouteNetworkElement => _queriedRouteNetworkElement;

        public RelatedSpanEquipment[] RelatedSpanEquipment
        {
            get
            {
                return _relatedSpanEquipment ?? Array.Empty<RelatedSpanEquipment>();
            }
            set
            {
                _relatedSpanEquipment = value;
            }
        }

        public SpanEquipmentSpecification[] SpanEquipmentSpecifications
        {
            get
            {
                return _spanEquipmentSpecifications ?? Array.Empty<SpanEquipmentSpecification>();
            }
            set
            {
                _spanEquipmentSpecifications = value;
            }
        }

        public void PopulateReferences()
        {
            PopulateSpanEquipmentSpecificationReferences();
        }

        private void PopulateSpanEquipmentSpecificationReferences()
        {
            // Create a dict for fast lookup of span equipment specifications
            Dictionary<Guid, SpanEquipmentSpecification> specificationDict = SpanEquipmentSpecifications.ToDictionary(i => i.Id);

            // Populate SpanEquipment->SpanEqupmentSpecification reference
            foreach (var spanEquipment in RelatedSpanEquipment)
            {
                // Populate reference from SpanEquipment to its SpanEqupmentSpecification object
                if (specificationDict.TryGetValue(spanEquipment.SpecificationId, out var spec))
                {
                    spanEquipment._specification = (SpanEquipmentSpecification)spec;
                }
                else
                {
                    throw new KeyNotFoundException($"Query result is invalid. Cannot find span equipment specification with id: {spanEquipment.SpecificationId}");
                }
            }
        }
    }
}
