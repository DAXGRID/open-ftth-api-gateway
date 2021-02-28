## Example Mutation

```graphql
mutation
{
  spanEquipment
  {
    placSpanEquipmentInRouteNetwork(
      spanEquipmentId: "6d7b267e-8908-4f98-a5e1-f17582e8af8d", 
      spanEquipmentSpecificationId: "7ca9dcbb-524f-4d61-945c-16bf2679326e",
      routeSegmentIds: [
        "72987aae-a1b1-4705-b4a8-bf2968238cf0",
        "19fa6101-785d-498b-97e5-a15e8e8d2745",
        "bf0034cd-f805-4f73-be68-bd5e18281565"
      ]
      manufacturerId: "47e87d16-a1f0-488a-8c3e-cb3a4f3e8926",
      markingInfo: {
        markingColor: "Red tape"
        markingText: "Super duper telco"
      },
      namingInfo: { 
        name: "rør", 
        description: "bla bla bla"
      }
    )
    {
      isSuccess
      errorCode
      errorMessage
    }
  }
}
```

## Params
ManufacturerId, markingInfo and namingInfo is optionally. The rest is mandatory.


## Description
The mutation first sends a RegisterWalkOfInterest command to the Route Network service.

Hereafter it sends a PlaceSpanEquipmentInRouteNetwork command to the Utility Network service.


## Ok result
```json
{
  "data": {
    "spanEquipment": {
      "placSpanEquipmentInRouteNetwork": {
        "isSuccess": true,
        "errorCode": null,
        "errorMessage": null
      }
    }
  }
}
```


## Failed result
```json
{
  "data": {
    "spanEquipment": {
      "placSpanEquipmentInRouteNetwork": {
        "isSuccess": false,
        "errorCode": "INVALID_SPAN_EQUIPMENT_ALREADY_EXISTS",
        "errorMessage": "INVALID_SPAN_EQUIPMENT_ALREADY_EXISTS: A span equipment with id: 6d7b267e-8908-4f98-a5e1-f17582e8af8d already exists."
      }
    }
  }
}
```

## Error Codes
[Register Walk Of Interest Error Codes](https://github.com/DAXGRID/open-ftth-route-network-service/blob/master/OpenFTTH.RouteNetwork.API/Commands/RegisterWalkOfInterestErrorCodes.cs)

[Place Span Equipment Error Codes](https://github.com/DAXGRID/open-ftth-utility-graph-service/blob/master/OpenFTTH.UtilityGraphService.API/Commands/PlaceSpanEquipmentInRouteNetworkErrorCodes.cs)





