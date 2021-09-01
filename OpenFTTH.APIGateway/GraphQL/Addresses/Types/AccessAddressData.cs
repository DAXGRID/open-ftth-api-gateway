using OpenFTTH.Address.API.Model;
using System;
using System.Collections.Generic;

namespace OpenFTTH.APIGateway.GraphQL.Addresses.Types
{
    public class AccessAddressData
    {
        public Guid Id { get; init; }
        public double Xwgs { get; init; }
        public double Ywgs { get; init; }
        public double Xetrs { get; init; }
        public double Yetrs { get; init; }
        public string? HouseNumber { get; init; }
        public string? PostDistrictCode { get; init; }
        public string? PostDistrict { get; init; }
        public Guid? ExternalId { get; init; }
        public string? RoadCode { get; init; }
        public string? RoadName { get; init; }
        public string? TownName { get; init; }
        public string? MunicipalCode { get; init; }

        public List<UnitAddress> UnitAddresses { get; init; }
    }
}
