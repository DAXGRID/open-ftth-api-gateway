## Example Query

```graphql
query {
  utilityNetwork {
    spanEquipmentSpecifications {
      id
      category
      name
      description
      deprecated
      manufacturerRefs
    }
  }
}
```

## Example result
```json
{
  "data": {
    "utilityNetwork": {
      "spanEquipmentSpecifications": [
        {
          "id": "7ca9dcbb-524f-4d61-945c-16bf2679326e",
          "category": "Conduit",
          "name": "Ø40 5x10",
          "description": "ø40 mm Multirør 5x10",
          "deprecated": false,
          "manufacturerRefs": [
            "47e87d16-a1f0-488a-8c3e-cb3a4f3e8926",
            "fd457db0-ad32-444c-9946-a9e5e8a14d17"
          ]
        },
        {
          "id": "f8d15ef6-b07f-440b-8357-4c7a3f84f156",
          "category": "Conduit",
          "name": "Ø40 6x10",
          "description": "ø40 mm Multirør 6x10",
          "deprecated": false
          "manufacturerRefs": [
            "47e87d16-a1f0-488a-8c3e-cb3a4f3e8926",
            "fd457db0-ad32-444c-9946-a9e5e8a14d17"
          ]
        },
        {
          "id": "b11a4fce-2116-4437-9108-3ca467124d99",
          "category": "Conduit",
          "name": "Ø32 3x10",
          "description": "ø32 mm Multirør 3x10",
          "deprecated": false,
          "manufacturerRefs": [
            "47e87d16-a1f0-488a-8c3e-cb3a4f3e8926",
            "fd457db0-ad32-444c-9946-a9e5e8a14d17"
          ]
        },
        {
          "id": "36f0deaf-0d77-4cae-be06-1e6e0cf84ae2",
          "category": "Conduit",
          "name": "Ø50 10x10",
          "description": "ø50 mm Multirør 5x10 + 12x7 color",
          "deprecated": false,
          "manufacturerRefs": [
            "47e87d16-a1f0-488a-8c3e-cb3a4f3e8926",
            "fd457db0-ad32-444c-9946-a9e5e8a14d17"
          ]
        },
        {
          "id": "1c2a1e9e-03e6-4eb9-ae89-e723fea1e59c",
          "category": "Conduit",
          "name": "Ø50 10x10",
          "description": "ø50 mm Multirør 10x10",
          "deprecated": false,
          "manufacturerRefs": [
            "47e87d16-a1f0-488a-8c3e-cb3a4f3e8926",
            "fd457db0-ad32-444c-9946-a9e5e8a14d17"
          ]
        }
      ]
    }
  }
}
```
