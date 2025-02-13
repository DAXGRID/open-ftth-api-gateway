using OpenFTTH.EventSourcing;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using System;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments
{
    public class ManufacturerAR : AggregateBase
    {
        public static readonly Guid UUID = Guid.Parse("de53b885-1a13-4282-8fd4-d6c9008ad413");

        private readonly LookupCollection<Manufacturer> _manufacturer = new LookupCollection<Manufacturer>();

        public ManufacturerAR()
        {
            Id = UUID;
            Register<ManufacturerAdded>(Apply);
        }

        private void Apply(ManufacturerAdded @event)
        {
            _manufacturer.Add(@event.Manufacturer);
        }

        public void AddManufacturer(CommandContext cmdContext, Manufacturer manufacturer)
        {
            if (String.IsNullOrEmpty(manufacturer.Name))
                throw new ArgumentException($"Manufacturer name cannot be null or empty");

            if (_manufacturer.ContainsKey(manufacturer.Id))
                throw new ArgumentException($"A manufacturer with id: {manufacturer.Id} already exists");

            if (_manufacturer.Any(m => m.Name == manufacturer.Name))
                throw new ArgumentException($"A manufacturer with name: {manufacturer.Name} already exists");

            RaiseEvent(
                new ManufacturerAdded(manufacturer)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );
        }
    }
}
