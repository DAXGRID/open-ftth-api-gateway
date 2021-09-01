using GraphQL.Types;

namespace OpenFTTH.APIGateway.GraphQL.Addresses.Types
{
    public class AccessAddressType : ObjectGraphType<AccessAddressData>
    {
        public AccessAddressType()
        {
            Field(x => x.Id, type: typeof(IdGraphType)).Description("Internal Address Id");
            Field(x => x.ExternalId, type: typeof(IdGraphType)).Description("External Address Id");
            Field(x => x.HouseNumber, type: typeof(StringGraphType)).Description("HouseNumber");
            Field(x => x.PostDistrictCode, type: typeof(StringGraphType)).Description("PostDistrictCode");
            Field(x => x.PostDistrict, type: typeof(StringGraphType)).Description("PostDistrict");
            Field(x => x.RoadCode, type: typeof(StringGraphType)).Description("RoadCode");
            Field(x => x.RoadName, type: typeof(StringGraphType)).Description("RoadName");
            Field(x => x.TownName, type: typeof(StringGraphType)).Description("TownName");
            Field(x => x.MunicipalCode, type: typeof(StringGraphType)).Description("MunicipalCode");
            Field(x => x.Xwgs, type: typeof(FloatGraphType)).Description("X coordinate in WGS84");
            Field(x => x.Ywgs, type: typeof(FloatGraphType)).Description("Y coordinate in WGS84");
            Field(x => x.Xetrs, type: typeof(FloatGraphType)).Description("X coordinate in ETRS89 UTM32");
            Field(x => x.Yetrs, type: typeof(FloatGraphType)).Description("Y coordinate in ETRS89 UTM32");

            Field(x => x.UnitAddresses, type: typeof(ListGraphType<UnitAddressType>)).Description("Unit Addresses belonging to this Access Address");
        }
    }
}
