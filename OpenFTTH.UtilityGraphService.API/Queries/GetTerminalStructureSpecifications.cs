using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetTerminalStructureSpecifications : IQuery<Result<LookupCollection<TerminalStructureSpecification>>> { };
}
