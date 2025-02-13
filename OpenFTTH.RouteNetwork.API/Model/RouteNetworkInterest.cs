using OpenFTTH.Core;
using System;

namespace OpenFTTH.RouteNetwork.API.Model
{
    /// <summary>
    /// Data transformer object holding point-of-interest or walk-of-interest information.
    /// </summary>
    public record RouteNetworkInterest : IIdentifiedObject
    {
        public Guid Id { get; }
        public RouteNetworkInterestKindEnum Kind { get; }
        public RouteNetworkElementIdList RouteNetworkElementRefs { get; init; }

        public string? Name => null;

        public string? Description => null;

        public RouteNetworkInterest(Guid id, RouteNetworkInterestKindEnum kind, RouteNetworkElementIdList routeNetworkElementRefs)
        {
            this.Id = id;
            this.Kind = kind;
            this.RouteNetworkElementRefs = routeNetworkElementRefs;
        }
    }
}

/* 
--------------------------------------------------------------- 
RouteNetworkDetailsQuery
--------------------------------------------------------------- 

IncludeInterestInformation : 
    None
    ReferenceFromRouteElementOnly
    InterestObject

RouteNetworkElements: [
    {
        "Id": "ef8fa1e9-ae6a-4d26-a2bf-4e9902c57237",
        "Kind": "RouteNode",
        "InterestRelations": [
            {
                InterestId: "6587f584-0f0f-47d9-b91d-6dc1c585a720",
                RelationType: "End"
            }
        ]
    }
]

Interests: [
    {
        "Id": "6587f584-0f0f-47d9-b91d-6dc1c585a720"
        "Kind": "NodeOfInterest",
        "SourceInfo": "ConduitSpliceClosure",
        "RouteNeworkElementIds": ["ef8fa1e9-ae6a-4d26-a2bf-4e9902c57237"]
    }
]


--------------------------------------------------------------- 
GetRouteNetworkDataByInterestIdsQuery QUERY
--------------------------------------------------------------- 

RouteNetworkElements: [
    {
        "Id": "adadasd",
        "Class": "RouteNode",
        "Coordinates": "asdada"
    }
]

Walks: [
    {
        "id": "asdadasd",
        "routeElementRefs": [0,3,4,4,3,2,4,56,4,3]
    }
]


--------------------------------------------------------------- 
GetEquipmentInfoByRouteNodeIdQuery QUERY
--------------------------------------------------------------- 

RouteNetworkElements: [
    {
        "Id": "adadasd",
        "Class": "RouteNode",
        "Coordinates": "asdasdas",

        "SpanEquipmentRelations": [
            {
                "Id": "12345"
                "Class": "MultiConduit"
                "RelType": "PassThrough"
                "SpanSegmentRelations": [
                    {
                        "Id": "12345"
                        "Class": "OuterConduitSegment"
                        "Type": "PassThrough"
                        "Walk": 2
                        "Pos": 1
                        "Children": [
                            {
                                "Id": "12345"
                                "Class": "InnerConduitSegment"
                                "Type": "Start"
                                "Pos": 1
                            }
                        ]
                    }
                ]
            }
        ]
    }
]

"SpanEquipmentRelations":
{
    "Class": "OuterCoduit"

}



*/