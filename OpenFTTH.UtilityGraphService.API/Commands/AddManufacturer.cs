using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using System;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public record AddManufacturer : BaseCommand, ICommand<Result>
    {
        public Manufacturer Manufacturer { get; }

        public AddManufacturer(Guid correlationId, UserContext userContext, Manufacturer manufacturer) : base(correlationId, userContext)
        {
            Manufacturer = manufacturer;
        }
    }
}
