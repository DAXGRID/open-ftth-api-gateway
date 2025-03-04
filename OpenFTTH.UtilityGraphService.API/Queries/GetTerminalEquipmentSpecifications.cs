﻿using OpenFTTH.Results;
using OpenFTTH.CQRS;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetTerminalEquipmentSpecifications : IQuery<Result<LookupCollection<TerminalEquipmentSpecification>>> { };
}
