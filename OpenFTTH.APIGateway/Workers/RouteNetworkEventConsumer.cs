using DAX.EventProcessing.Dispatcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenFTTH.APIGateway.Conversion;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Business.RouteElements.EventHandling;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Topos.Config;
using Topos.InMem;

namespace OpenFTTH.APIGateway.Workers
{
    public class RouteNetworkEventConsumer : BackgroundService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RouteNetworkEventConsumer> _logger;
        private readonly IEventStore _eventStore;
        private readonly IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent> _eventDispatcher;
        private readonly RouteNetworkEventHandler _routeNetworkEventHandler;
        private readonly IRouteNetworkState _routeNetworkState;
        private readonly KafkaSetting _kafkaSetting;
        private readonly EventStoreDatabaseSetting _eventStoreDatabaseSetting;
        private readonly GeoDatabaseSetting _geoDatabaseSetting;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        private InMemPositionsStorage _positionsStorage = new InMemPositionsStorage();
        private IDisposable _kafkaConsumer;

        public RouteNetworkEventConsumer(ILoggerFactory loggerFactory, IEventStore eventStore, IOptions<KafkaSetting> kafkaSetting, IOptions<EventStoreDatabaseSetting> eventStoreDatabaseSetting, IOptions<GeoDatabaseSetting> geoDatabaseSetting, IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent> eventDispatcher, RouteNetworkEventHandler routeNetworkEventHandler, IRouteNetworkState routeNetworkState, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<RouteNetworkEventConsumer>();
            _eventStore = eventStore;
            _kafkaSetting = kafkaSetting.Value;
            _eventStoreDatabaseSetting = eventStoreDatabaseSetting.Value;
            _geoDatabaseSetting = geoDatabaseSetting.Value;

            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            _eventDispatcher = eventDispatcher;
            _routeNetworkEventHandler = routeNetworkEventHandler;
            _routeNetworkState = routeNetworkState;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting route network event consumer worker at: {time}", DateTimeOffset.Now);

            try
            {
                if (String.IsNullOrEmpty(_kafkaSetting.RouteNetworkEventTopic))
                    throw new ApplicationException("RouteNetworkEventTopic Kafka app setting must be set!");

                var toposConfig = _eventDispatcher.Config("route_network_event_" + Guid.NewGuid(), c =>
                {
                    var kafkaConfig = c.UseKafka(_kafkaSetting.Server);

                    if (_kafkaSetting.CertificateFilename != null)
                    {
                        kafkaConfig.WithCertificate(_kafkaSetting.CertificateFilename);
                    }
                })
               .Logging(l => l.UseSerilog())
               .Positions(p => p.StoreInMemory(_positionsStorage))
               .Topics(t => t.Subscribe(_kafkaSetting.RouteNetworkEventTopic));

                _eventDispatcher.OnEvent.Subscribe(_routeNetworkEventHandler);

                _kafkaConsumer = toposConfig.Start();

                var inMemRouteNetworkState = (InMemRouteNetworkState)_routeNetworkState;


                // Wait for load mode to create an initial version/state
                _logger.LogInformation("Starting route network events load mode...");

                bool loadFinish = false;

                if (loadFinish)
                    _logger.LogError("NOTICE THAT WAITING FOR ROUTE EVENTS LOADING IS DISABLED!!!");

                while (!stoppingToken.IsCancellationRequested && !loadFinish)
                {
                    _logger.LogDebug("Waiting for load mode to finish creating initial state...");

                    _logger.LogInformation($"{inMemRouteNetworkState.NumberOfObjectsLoaded} route network Kafka events processed.");

                    DateTime waitStartTimestamp = DateTime.UtcNow;

                    await Task.Delay(5000, stoppingToken);

                    TimeSpan timespan = waitStartTimestamp - inMemRouteNetworkState.LastEventRecievedTimestamp;

                    if (timespan.TotalSeconds > 20)
                    {
                        loadFinish = true;
                    }
                }


                ((InMemRouteNetworkState)_routeNetworkState).FinishLoadMode();
                _logger.LogInformation("Loading of initial route network state finished.");

                // Dehydrate projections
                LogMenUsage();

                _logger.LogInformation("Start dehydrate in-memory projections...");
                _eventStore.DehydrateProjections();
                _logger.LogInformation("Finish dehydrating in-memory projections.");

                LogMenUsage();

                // We are now ready to serve the public if the loaded objects are bigger than 0
                if (inMemRouteNetworkState.NumberOfObjectsLoaded > 0)
                {
                    File.Create("/tmp/healthy");
                    _logger.LogInformation("Healhty file written writen to tmp.");
                }
                else
                    throw new ApplicationException("Recieved no route network elements from Kafka topic.");

                // Start conversion
                new ConversionRunner(_loggerFactory, _eventStore, _geoDatabaseSetting, _commandDispatcher, _queryDispatcher).Run();


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

            await Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping background worker");
            _kafkaConsumer.Dispose();

            await Task.CompletedTask;
        }

        private double LogMenUsage()
        {
            var me = Process.GetCurrentProcess();

            _logger.LogInformation($"Memory usage current process id: {me.Id} {me.WorkingSet64 / 1024.0 / 1024.0 / 1024.0} GB");

            return me.WorkingSet64;
        }
    }
}
