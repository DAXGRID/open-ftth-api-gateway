using DAX.EventProcessing.Dispatcher;
using OpenFTTH.Results;
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
        private readonly ITypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> _typedEventObversable;
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ObserverSubject>> _observableByRouteNetworkElementId = new();

        public TerminalEquipmentConnectivityObserver(
            ILogger<TerminalEquipmentConnectivityObserver> logger,
            ITypedEventObservable<RouteNetworkElementContainedEquipmentUpdated> typedEventObservable,
            IQueryDispatcher queryDispatcher)
        {
            _logger = logger;
            _typedEventObversable = typedEventObservable;
            _queryDispatcher = queryDispatcher;
            _typedEventObversable.OnEvent.Subscribe(this);
        }

        public IObservable<TerminalEquipmentAZConnectivityViewModel> WhenViewNeedsUpdate(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            return GetObservable(routeNodeId, terminalEquipmentOrRackId).Subject.AsObservable();
        }

        private ObserverSubject GetObservable(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            var subject = new ObserverSubject(new Subject<TerminalEquipmentAZConnectivityViewModel>(), terminalEquipmentOrRackId, routeNodeId);

            var observableList = _observableByRouteNetworkElementId.GetOrAdd(routeNodeId, new ConcurrentDictionary<Guid, ObserverSubject>());

            if (observableList.TryGetValue(terminalEquipmentOrRackId, out var observerSubject))
            {
                return observerSubject;
            }
            else
            {
                observableList.TryAdd(terminalEquipmentOrRackId, subject);
                return subject;
            }
        }

        void IObserver<RouteNetworkElementContainedEquipmentUpdated>.OnNext(RouteNetworkElementContainedEquipmentUpdated @event)
        {
            foreach (var routeNetworkElementId in @event.AffectedRouteNetworkElementIds)
            {
                if (_observableByRouteNetworkElementId.TryGetValue(routeNetworkElementId, out var observers))
                {
                    if (@event.Category == "EquipmentDeletion")
                    {
                        if (@event.IdChangeSets != null && @event.IdChangeSets.Any(i => i.ObjectType == "TerminalEquipment"))
                        {
                            var idChangeSet = @event.IdChangeSets.First(i => i.ObjectType == "TerminalEquipment");

                            foreach (var terminalEquipmentId in idChangeSet.IdList)
                            {
                                if (observers.ContainsKey(terminalEquipmentId))
                                {
                                    if (observers.TryRemove(terminalEquipmentId, out var observerSubject))
                                    {
                                        if (!observerSubject.Subject.IsDisposed)
                                            observerSubject.Subject.Dispose();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var observer in observers.Values)
                        {
                            observer.Subject.OnNext(GetTerminalEquipmentConnectivity(routeNetworkElementId, observer.TerminalEquipmentOrRackId));
                        }
                    }
                }
            }
        }

        private TerminalEquipmentAZConnectivityViewModel GetTerminalEquipmentConnectivity(Guid routeNodeId, Guid terminalEquipmentOrRackId)
        {
            // We catch all exceptions to avoid Topos retrying (calling the message handler again and again)
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


        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            _logger.LogError("{Exception}", error);
        }

        private class ObserverSubject
        {
            public Subject<TerminalEquipmentAZConnectivityViewModel> Subject { get; set; }
            public Guid TerminalEquipmentOrRackId { get; set; }
            public Guid RouteNodeId { get; set; }

            public ObserverSubject(Subject<TerminalEquipmentAZConnectivityViewModel> subject, Guid terminalEquipmentOrRackId, Guid routeNodeId)
            {
                Subject = subject;
                TerminalEquipmentOrRackId = terminalEquipmentOrRackId;
                RouteNodeId = routeNodeId;
            }
        }
    }
}
