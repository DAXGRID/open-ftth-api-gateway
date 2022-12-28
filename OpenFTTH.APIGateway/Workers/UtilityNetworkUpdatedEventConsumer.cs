using DAX.EventProcessing.Dispatcher;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.NotificationClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Workers
{
    public class UtilityNetworkUpdatedEventConsumer : BackgroundService
    {
        private readonly ITypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> _eventDispatcher;
        private readonly Client _notificationClient;

        public UtilityNetworkUpdatedEventConsumer(
            ITypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> eventDispatcher,
            OpenFTTH.NotificationClient.Client notificationClient)
        {
            _eventDispatcher = eventDispatcher;
            _notificationClient = notificationClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var notificationCh = _notificationClient.Connect();

            var notifications = notificationCh.ReadAllAsync().ConfigureAwait(false);

            await foreach (var notification in notifications)
            {
                if (string.CompareOrdinal(notification.Type, "RouteNetworkElementContainedEquipmentUpdated") == 0)
                {
                    var areaUpdated = JsonConvert
                        .DeserializeObject<RouteNetworkElementContainedEquipmentUpdated>(notification.Body);

                    if (areaUpdated is null)
                    {
                        throw new InvalidOperationException(
                            $"Could not deserialize {nameof(RouteNetworkElementContainedEquipmentUpdated)} resulted in null.");
                    }

                    _eventDispatcher.Dispatch(areaUpdated);
                }
            }
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _notificationClient.Dispose();
            return Task.CompletedTask;
        }
    }
}
