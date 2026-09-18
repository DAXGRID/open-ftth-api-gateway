using Baseline.ImTools;
using OpenFTTH.Core.Address;
using OpenFTTH.Core.Address.Events;
using OpenFTTH.EventSourcing;
using OpenFTTH.Installation.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.UtilityGraphService.Business.Graph.Projections
{
    public class InstallationProjection : ProjectionBase
    {
        private readonly ConcurrentDictionary<Guid, InstallationRecord> _installationById = new();

        private readonly ConcurrentDictionary<string, InstallationRecord> _installationByInstallationId = new();

        public InstallationProjection()
        {
            ProjectEventAsync<InstallationCreated>(ProjectAsync);
            ProjectEventAsync<InstallationStatusChanged>(ProjectAsync);
            ProjectEventAsync<InstallationUnitAddressChanged>(ProjectAsync);
            ProjectEventAsync<InstallationLocationRemarkChanged>(ProjectAsync);
            ProjectEventAsync<InstallationRemarkChanged>(ProjectAsync);
        }

        public IReadOnlyDictionary<Guid, InstallationRecord> InstallationsById
        {
            get { return _installationById; }
        }

        public InstallationRecord? GetInstallationInfo(string name, UtilityNetworkProjection utilityNetwork)
        {
            if (String.IsNullOrEmpty(name))
                return null;

            if (_installationByInstallationId.TryGetValue(name, out InstallationRecord installation))
            {
                return installation;
            }

            return null;
        }

        private Task ProjectAsync(IEventEnvelope eventEnvelope)
        {
            switch (eventEnvelope.Data)
            {
                case (InstallationCreated @event):
                    var newInst = new InstallationRecord(@event.Id)
                    {
                        InstallationId = @event.InstallationId,
                        UnitAddressId = @event.UnitAddressId,
                        Status = @event.Status,
                        Remark = @event.Remark,
                        LocationRemark = @event.LocationRemark
                    };

                    _installationById.TryAdd(@event.Id, newInst);
                    _installationByInstallationId.TryAdd(@event.InstallationId, newInst);

                    break;

                case (InstallationStatusChanged @event):
                    _installationById[@event.Id].Status = @event.Status;
                    break;

                case (InstallationUnitAddressChanged @event):
                    _installationById[@event.Id].UnitAddressId = @event.UnitAddressId;
                    break;

                case (InstallationLocationRemarkChanged @event):
                    _installationById[@event.Id].LocationRemark = @event.LocationRemark;
                    break;

                case (InstallationRemarkChanged @event):
                    _installationById[@event.Id].Remark = @event.Remark;
                    break;
            }


            return Task.CompletedTask;
        }
    }

    public record InstallationRecord
    {
        public Guid Id { get; set; }
        public string? InstallationId { get; set; }
        public string? Status { get; set; }
        public string? Remark { get; set; }
        public string? LocationRemark { get; set; }
        public Guid? UnitAddressId { get; set; }

        public InstallationRecord(Guid id)
        {
            Id = id;
        }
    }

}




