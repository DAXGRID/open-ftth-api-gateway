using DAX.EventProcessing.Dispatcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenFTTH.APIGateway.Conversion;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Workers
{
    public class RouteNetworkEventConsumer : BackgroundService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RouteNetworkEventConsumer> _logger;
        private readonly IEventStore _eventStore;
        private readonly IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent> _eventDispatcher;
        private readonly IRouteNetworkState _routeNetworkState;
        private readonly EventStoreDatabaseSetting _eventStoreDatabaseSetting;
        private readonly GeoDatabaseSetting _geoDatabaseSetting;
        private readonly ICommandDispatcher _commandDispatcher;
        private readonly IQueryDispatcher _queryDispatcher;

        public RouteNetworkEventConsumer(
            ILoggerFactory loggerFactory,
            IEventStore eventStore,
            IOptions<EventStoreDatabaseSetting> eventStoreDatabaseSetting,
            IOptions<GeoDatabaseSetting> geoDatabaseSetting,
            IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent> eventDispatcher,
            IRouteNetworkState routeNetworkState,
            ICommandDispatcher commandDispatcher,
            IQueryDispatcher queryDispatcher)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<RouteNetworkEventConsumer>();
            _eventStore = eventStore;
            _eventStoreDatabaseSetting = eventStoreDatabaseSetting.Value;
            _geoDatabaseSetting = geoDatabaseSetting.Value;

            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;

            _eventDispatcher = eventDispatcher;
            _routeNetworkState = routeNetworkState;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Starting route network event consumer worker at: {time}",
                DateTimeOffset.Now);

            try
            {
                // Memory before
                LogMenUsage();

                var inMemRouteNetworkState = (InMemRouteNetworkState)_routeNetworkState;

                // Dehydrate projections
                _logger.LogInformation("Start dehydrate in-memory projections...");
                _eventStore.DehydrateProjections();
                _logger.LogInformation(
                    $"{inMemRouteNetworkState.NumberOfObjectsLoaded} route network events processed.");
                _logger.LogInformation("Finish dehydrating in-memory projections.");
                inMemRouteNetworkState.FinishLoadMode();

                // Memory after events are loaded
                LogMenUsage();

                // We are now ready to serve the public
                File.Create("/tmp/healthy");
                _logger.LogInformation("Healhty file written writen to tmp.");

                // Start conversion
                new ConversionRunner(
                    _loggerFactory,
                    _eventStore,
                    _geoDatabaseSetting,
                    _commandDispatcher,
                    _queryDispatcher).Run();

                // Catchup external events
                _logger.LogInformation("Start catching up external events...");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(100);

                        var eventsProcessed = _eventStore.CatchUp();

                        if (eventsProcessed > 0)
                        {
                            _logger.LogDebug(
                                $"Processed {eventsProcessed} new external events.");
                        }
                    }
                    catch (PostgresException ex)
                    {
                        const int RETRY_WAIT_TIME_SEC = 30;
                        _logger.LogError("{}", ex);

                        _logger.LogInformation(
                            "Waiting {Seconds}, before retrying catchup.",
                            RETRY_WAIT_TIME_SEC);

                        await Task.Delay(TimeSpan.FromSeconds(RETRY_WAIT_TIME_SEC));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stopping background worker");
            return Task.CompletedTask;
        }

        private void LogMenUsage()
        {
            var currentProcess = Process.GetCurrentProcess();
            _logger.LogInformation($"Memory usage current process id: {currentProcess.Id} {currentProcess.WorkingSet64 / 1024.0 / 1024.0 / 1024.0} GB");
        }
    }
}
