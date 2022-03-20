using DAX.EventProcessing.Dispatcher;
using FluentResults;
using Microsoft.Extensions.Logging;
using OpenFTTH.CQRS;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.Views;
using OpenFTTH.UtilityGraphService.API.Queries;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Subscriptions
{
    public class TerminalEquipmentConnectivityObserver : IObserver<RouteNetworkElementContainedEquipmentUpdated>
    {
        private readonly ILogger<TerminalEquipmentConnectivityObserver> _logger;
        private readonly IToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> _toposTypedEventObserable;
        private readonly IQueryDispatcher _queryDispatcher;

        private ConcurrentDictionary<Guid, ObserverSubject> _observableByRouteNetworkElementId = new ConcurrentDictionary<Guid, ObserverSubject>();

        public TerminalEquipmentConnectivityObserver(ILogger<TerminalEquipmentConnectivityObserver> logger, IToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> toposTypedEventObserable, IQueryDispatcher queryDispatcher)
        {
            _logger = logger;
            _toposTypedEventObserable = toposTypedEventObserable;
            _queryDispatcher = queryDispatcher;
            _toposTypedEventObserable.OnEvent.Subscribe(this);
        }

        public IObservable<TerminalEquipmentAZConnectivityViewModel> WhenViewNeedsUpdate(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            return GetObservable(routeNodeId, terminalEquipmentOrRackId).Subject.AsObservable();
        }

        private ObserverSubject GetObservable(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            var subject = new ObserverSubject(new Subject<TerminalEquipmentAZConnectivityViewModel>(), terminalEquipmentOrRackId);

            var observable = _observableByRouteNetworkElementId.GetOrAdd(routeNodeId, subject);
            observable.TerminalEquipmentOrRackId = terminalEquipmentOrRackId;

            return observable;

        }

        public void Ping(Guid routeNetworkElementId)
        {
            if (_observableByRouteNetworkElementId.TryGetValue(routeNetworkElementId, out var observable))
            {
                observable.Subject.OnNext(GetTerminalEquipmentConnectivity(routeNetworkElementId, observable.TerminalEquipmentOrRackId));
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        void IObserver<RouteNetworkElementContainedEquipmentUpdated>.OnNext(RouteNetworkElementContainedEquipmentUpdated @event)
        {
            foreach (var routeNetworkElementId in @event.AffectedRouteNetworkElementIds)
            {
                if (_observableByRouteNetworkElementId.TryGetValue(routeNetworkElementId, out var observable))
                {
                    observable.Subject.OnNext(GetTerminalEquipmentConnectivity(routeNetworkElementId, observable.TerminalEquipmentOrRackId));
                }
            }
        }

        private TerminalEquipmentAZConnectivityViewModel GetTerminalEquipmentConnectivity(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            // We catch all execeptions to avoid Topos retrying (calling the message handler again and again)
            // It does not matter that the failed event is never processed again, because it's just a notification topic
            try
            {
                var connectivityQuery = new GetTerminalEquipmentConnectivityView(routeNodeId, terminalEquipmentOrRackId);

                var connectivityQueryResult = _queryDispatcher.HandleAsync<GetTerminalEquipmentConnectivityView, Result<TerminalEquipmentAZConnectivityViewModel>>(
                    connectivityQuery
                ).Result;

                if (connectivityQueryResult.IsFailed)
                {
                    _logger.LogError($"Error getting terminal equipment connecitivity for equipment with id: {terminalEquipmentOrRackId} Failed with message: {connectivityQueryResult.Errors.First().Message}");
                    return null;
                }

                return connectivityQueryResult.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Getting terminal equipment connecitivity for equipment with id: {terminalEquipmentOrRackId} Failed with message: {ex.Message}", ex);
                return null;
            }
        }


        private class ObserverSubject
        {
            public Subject<TerminalEquipmentAZConnectivityViewModel> Subject { get; set; }
            public Guid TerminalEquipmentOrRackId { get; set; }

            public ObserverSubject(Subject<TerminalEquipmentAZConnectivityViewModel> subject, Guid terminalEquipmentOrRackId)
            {
                Subject = subject;
                TerminalEquipmentOrRackId = terminalEquipmentOrRackId;
            }
        }
    }
}
