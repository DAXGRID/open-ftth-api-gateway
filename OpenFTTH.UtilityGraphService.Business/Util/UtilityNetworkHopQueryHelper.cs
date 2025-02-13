using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.API.Queries;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.Util
{
    public class UtilityNetworkHopQueryHelper
    {
        private readonly IQueryDispatcher _queryDispatcher;
        private readonly UtilityNetworkProjection _utilityNetwork;

        public UtilityNetworkHopQueryHelper(IQueryDispatcher queryDispatcher, UtilityNetworkProjection utilityNetwork)
        {
            _queryDispatcher = queryDispatcher;
            _utilityNetwork = utilityNetwork;
        }

        public List<UtilitySubHop> GetSubwalks(UtilityNetworkHop utilityNetworkHop)
        {
            List<UtilitySubHop> subHopsToResult = new();

            HashSet<Guid> walkOfInterestsToQuery = new();

            List<IUtilityGraphSegmentRef> segmentRefsToProcess = new();

            // Get all span equipment/segments from utility network
            foreach (var parentAffix in utilityNetworkHop.ParentAffixes)
            {
                if (!_utilityNetwork.Graph.TryGetGraphElement<IUtilityGraphSegmentRef>(parentAffix.SpanSegmentId, out var utilityGraphSegmentRef))
                   throw new ApplicationException($"Cannot find span segment with id: {parentAffix.SpanSegmentId}");

                segmentRefsToProcess.Add(utilityGraphSegmentRef);

                var spanEquipment = utilityGraphSegmentRef.SpanEquipment(_utilityNetwork);
                walkOfInterestsToQuery.Add(spanEquipment.WalkOfInterestId);
            }

            // Query walk of interests 
            var walkOfInterestsByInterestId = GetWalkOfInterests(walkOfInterestsToQuery);

            // Process all segment
            foreach (var segmentRef in segmentRefsToProcess)
            {
                var spanEquipment = segmentRef.SpanEquipment(_utilityNetwork);
                var spanSegment = segmentRef.SpanSegment(_utilityNetwork);

                var spanSegmentWalk = GetSpanSegmentWalk(spanEquipment, spanSegment, walkOfInterestsByInterestId[spanEquipment.WalkOfInterestId]);

                var subwalk = CreateSubHopAndReverseWalkIfNessesary(utilityNetworkHop, subHopsToResult, spanSegment, spanSegmentWalk);

                subHopsToResult.Add(subwalk);
            }

            return subHopsToResult;
        }

        private UtilitySubHop CreateSubHopAndReverseWalkIfNessesary(UtilityNetworkHop utilityNetworkHop, List<UtilitySubHop> subHopsProcessed, SpanSegment spanSegment, ValidatedRouteNetworkWalk spanSegmentWalk)
        {
            if (subHopsProcessed.Count == 0)
            {
                if (spanSegmentWalk.FromNodeId == utilityNetworkHop.FromNodeId)
                {
                    return new UtilitySubHop(spanSegment.Id, spanSegmentWalk);
                }
                else if (spanSegmentWalk.ToNodeId == utilityNetworkHop.FromNodeId)
                {
                    return new UtilitySubHop(spanSegment.Id, spanSegmentWalk.Reverse());
                }
                else
                {
                    throw new ApplicationException($"Error processing sub walks of utility network hop. The walk of segment with id: {spanSegment.Id} was expected to either start or end at from route node: {utilityNetworkHop.FromNodeId} specified in the utility hop, but was not!");
                }
            }
            else
            {
                var prevHop = subHopsProcessed.Last();

                if (spanSegmentWalk.FromNodeId == prevHop.Walk.ToNodeId)
                {
                    return new UtilitySubHop(spanSegment.Id, spanSegmentWalk);
                }
                else if (spanSegmentWalk.ToNodeId == prevHop.Walk.ToNodeId)
                {
                    return new UtilitySubHop(spanSegment.Id, spanSegmentWalk.Reverse());
                }
                else
                {
                    throw new ApplicationException($"Error processing sub walks of utility network hop. The walk of segment with id: {spanSegment.Id} was expected to either start or end at walk of previous segment with id: {prevHop.ParentSegementId}, but was not! Seems that the two segments are no longer adjacent");
                }
            }
        }

        private ValidatedRouteNetworkWalk GetSpanSegmentWalk(SpanEquipment spanEquipment, SpanSegment spanSegment, ValidatedRouteNetworkWalk spanEquipmentWalkOfInterest)
        {

            var fromNodeId = spanEquipment.NodesOfInterestIds[spanSegment.FromNodeOfInterestIndex];
            var toNodeId = spanEquipment.NodesOfInterestIds[spanSegment.ToNodeOfInterestIndex];

            RouteNetworkElementIdList result = new();

            bool fromNodeFound = false;
            bool toNodeFound = false;

            for (int woiIndex = 0; woiIndex < spanEquipmentWalkOfInterest.RouteNetworkElementRefs.Count; woiIndex++)
            {
                if (spanEquipmentWalkOfInterest.RouteNetworkElementRefs[woiIndex] == fromNodeId)
                {
                    fromNodeFound = true;
                }

                if (fromNodeFound)
                    result.Add(spanEquipmentWalkOfInterest.RouteNetworkElementRefs[woiIndex]);

                if (spanEquipmentWalkOfInterest.RouteNetworkElementRefs[woiIndex] == toNodeId)
                {
                    toNodeFound = true;
                    break;
                }
            }

            if (!fromNodeFound)
            {
                throw new ApplicationException($"Error creating subwalk from node: {fromNodeId} to node: {toNodeId} in span equipment with id: {spanEquipment.Id} The from node id was never found! There must be inconsistency in span segment with id: {spanSegment.Id}");
            }

            if (!toNodeFound)
            {
                throw new ApplicationException($"Error creating subwalk from node: {fromNodeId} to node: {toNodeId} in span equipment with id: {spanEquipment.Id} The to node id was never found! There must be inconsistency in span segment with id: {spanSegment.Id}");
            }

            return new ValidatedRouteNetworkWalk(result);
        }

        private Dictionary<Guid, ValidatedRouteNetworkWalk> GetWalkOfInterests(IEnumerable<Guid> interestIds)
        {
            Dictionary<Guid, ValidatedRouteNetworkWalk> result = new();

            InterestIdList interestIdList = new();

            interestIdList.AddRange(interestIds);

            // Get interest information from existing span equipment
            var interestQueryResult = _queryDispatcher.HandleAsync<GetRouteNetworkDetails, Result<GetRouteNetworkDetailsResult>>(
                new GetRouteNetworkDetails(interestIdList)
            ).Result;

            if (interestQueryResult.IsFailed)
                throw new ApplicationException($"Got an unexpected error querying route network element by interest ids: {interestQueryResult.Errors.First().Message}");

            if (interestQueryResult.Value.Interests != null)
            {
                foreach (var interest in interestQueryResult.Value.Interests)
                {
                    RouteNetworkElementIdList routeNetworkElementIds = new RouteNetworkElementIdList();
                    routeNetworkElementIds.AddRange(interest.RouteNetworkElementRefs);

                    result.Add(interest.Id, new ValidatedRouteNetworkWalk(routeNetworkElementIds));
                }
            }

            return result;
        }
    }


    public class UtilitySubHop 
    {
        public Guid ParentSegementId { get; }
        public ValidatedRouteNetworkWalk Walk { get; }

        public UtilitySubHop(Guid parentSegementId, ValidatedRouteNetworkWalk walk)
        {
            ParentSegementId = parentSegementId;
            Walk = walk;
        }
    }
}
