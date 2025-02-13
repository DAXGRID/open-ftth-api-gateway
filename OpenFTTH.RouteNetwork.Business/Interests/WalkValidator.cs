using FluentResults;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.Model;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.RouteNetwork.Business.Interest
{
    public class WalkValidator
    {
        readonly IRouteNetworkRepository _routeNetworkRepository;

        public WalkValidator(IRouteNetworkRepository routeNetworkRepository)
        {
            _routeNetworkRepository = routeNetworkRepository;
        }

        public Result<RouteNetworkElementIdList> ValidateWalk(RouteNetworkElementIdList walkIds)
        {
            long versionId = _routeNetworkRepository.NetworkState.GetLatestCommitedVersion();

            var lookupRouteNetworkObjectsResult = LookupRouteNetworkObjects(walkIds, versionId);

            // If some route network element could not be looked up then return failure
            if (lookupRouteNetworkObjectsResult.IsFailed)
                return Result.Fail<RouteNetworkElementIdList>(lookupRouteNetworkObjectsResult.Errors.First());

            // If only one id is specified, make sure it'a a route segment
            if (lookupRouteNetworkObjectsResult.Value.Count == 1 && !(lookupRouteNetworkObjectsResult.Value[0] is IRouteSegment))
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SHOULD_CONTAIN_ROUTE_SEGMENT_IDS_ONLY, "If only one route network id is specified in a walk, it must be a route segment id"));
          
            var routeElementsSummary = GetRouteNetworkElementsListSummary(lookupRouteNetworkObjectsResult.Value);

            switch (routeElementsSummary)
            {
                case RouteElementListSummary.None:
                    return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_CANNOT_BE_EMPTY, "A valid walk should contain at least one route segment id"));

                case RouteElementListSummary.RouteNodesOnly:
                    return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SHOULD_CONTAIN_ROUTE_SEGMENT_IDS_ONLY, "A valid walk cannot contain route nodes only."));

                case RouteElementListSummary.RouteSegmentsOnly:
                    var sortedRouteNetworkObjectsResult = SortRouteSegments(lookupRouteNetworkObjectsResult.Value.OfType<RouteSegment>().ToList(), versionId);

                    if (sortedRouteNetworkObjectsResult.IsFailed)
                        return Result.Fail<RouteNetworkElementIdList>(sortedRouteNetworkObjectsResult.Errors.First());

                    var routeSegments = sortedRouteNetworkObjectsResult.Value.OfType<RouteSegment>().ToList();
                    return ValidateSegmentSequence(routeSegments, versionId);

                case RouteElementListSummary.BothRouteNodesAndSegments:
                    var validateResult = ValidateNodeSegmentSequence(lookupRouteNetworkObjectsResult.Value, versionId);
                    if (validateResult.IsSuccess)
                    {
                        return Result.Ok(walkIds);
                    }
                    else
                        return Result.Fail(validateResult.Errors.First());
            }

            return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SHOULD_CONTAIN_ROUTE_SEGMENT_IDS_ONLY, "Unsupported type of route network id sequence"));
        }


        private Result<RouteNetworkElementIdList> ValidateSegmentSequence(List<RouteSegment> routeSegments, long versionId)
        {
            var routeSegmentPosition = 0;

            foreach (var routeSegment in routeSegments)
            {
                if (routeSegmentPosition > 0)
                {
                    var prevSegment = routeSegments[routeSegmentPosition - 1];

                    if (!IsAdjacent(prevSegment, routeSegment, versionId))
                        return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SEGMENTS_ARE_NOT_ADJACENT, $"Segments is out of sequence and/or not adjacent to eatch other. Segment with id: {routeSegment.Id} was expected to be adjacent to segment with id: {prevSegment.Id} in the route network graph - but was not."));
                }

                routeSegmentPosition++;
            }

            return Result.Ok<RouteNetworkElementIdList>(CreateWalkFromSegmentSequence(routeSegments, versionId));
        }

        private Result ValidateNodeSegmentSequence(List<IRouteNetworkElement> routeNetworkElements, long versionId)
        {
            if (!(routeNetworkElements.First() is RouteNode))
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_FIRST_ROUTE_NETWORK_ELEMENT_MUST_BE_A_NODE, $"First route network element with id: {routeNetworkElements.First().Id} was expected to be a node."));

            if (!(routeNetworkElements.Last() is RouteNode))
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_LAST_ROUTE_NETWORK_ELEMENT_MUST_BE_A_NODE, $"Last route network element with id: {routeNetworkElements.First().Id} was expected to be a node."));

            var routeElementPosition = 0;

            foreach (var currentRouteNetworkElement in routeNetworkElements)
            {
                if (routeElementPosition > 0)
                {
                    var prevElement = routeNetworkElements[routeElementPosition - 1];

                    if (!IsAdjacent(prevElement, currentRouteNetworkElement, versionId))
                        return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_ROUTE_NETWORK_ELEMENTS_ARE_NOT_ADJACENT, $"Route nodes or segments is out of sequence and/or not adjacent to each other. Route network element with id: {currentRouteNetworkElement.Id} was expected to be adjacent to route network element with id: {prevElement.Id} in the route network graph - but was not."));

                    if (!IsDifferentType(prevElement, currentRouteNetworkElement, versionId))
                        return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_ADJACENT_ROUTE_NETWORK_ELEMENTS_ARE_OF_SAME_TYPE, $"Route network element with id: {currentRouteNetworkElement.Id} adjacent to route network element with id: {prevElement.Id} is the same type. Expected node->segment->node->segment->node..."));

                }

                routeElementPosition++;
            }

            return Result.Ok();
        }

        private RouteNetworkElementIdList CreateWalkFromSegmentSequence(List<RouteSegment> routeSegments, long versionId)
        {
            RouteNetworkElementIdList walkIds = new RouteNetworkElementIdList();

            var routeSegmentPosition = 0;

            // If only one route segment, inV and outV will be the node sequence
            if (routeSegments.Count == 1)
            {
                walkIds.Add(routeSegments[0].InV(versionId).Id);
                walkIds.Add(routeSegments[0].Id);
                walkIds.Add(routeSegments[0].OutV(versionId).Id);

                return walkIds;
            }

            // We're dealing with multi segments, if we reach this code
            foreach (var routeSegment in routeSegments)
            {
                if (routeSegmentPosition > 0)
                {
                    var prevSegment = routeSegments[routeSegmentPosition - 1];

                    var sharedRouteNode = FindSharedNode(prevSegment, routeSegment, versionId);

                    // If prevSegment is the first segment then remember to add it incl. the first node and shared node
                    if (routeSegmentPosition == 1)
                    {
                        if (prevSegment.InV(versionId) != sharedRouteNode)
                            walkIds.Add(prevSegment.InV(versionId).Id);
                        else
                            walkIds.Add(prevSegment.OutV(versionId).Id);

                        // Add prev segment
                        walkIds.Add(prevSegment.Id);

                        // Add shared node
                        walkIds.Add(sharedRouteNode.Id);
                    }

                    // Add current segment and non shared node
                    walkIds.Add(routeSegment.Id);

                    if (routeSegment.InV(versionId) != sharedRouteNode)
                        walkIds.Add(routeSegment.InV(versionId).Id);
                    else
                        walkIds.Add(routeSegment.OutV(versionId).Id);
                }

                routeSegmentPosition++;
            }

            return walkIds;
        }

        private RouteNode FindSharedNode(RouteSegment segment1, RouteSegment segment2, long versionId)
        {
            foreach (var neighborNode in segment1.NeighborElements(versionId))
            {
                if (segment2.NeighborElements(versionId).Contains(neighborNode))
                    return (RouteNode)neighborNode;
            }

            throw new ApplicationException("FindSharedNode should never be called on an unvalidated pair of segments.");
        }

        private static bool IsAdjacent(RouteSegment segment1, RouteSegment segment2, long versionId)
        {
            foreach (var neighboor in segment1.NeighborElements(versionId))
            {
                if (neighboor.NeighborElements(versionId).Contains(segment2))
                    return true;
            }

            return false;
        }

        private static bool IsAdjacent(IRouteNetworkElement element1, IRouteNetworkElement element2, long versionId)
        {
            if (element1 is RouteNode routeNode)
            {
                if (routeNode.NeighborElements(versionId).Contains(element2))
                    return true;
            }

            if (element1 is RouteSegment routeSegment)
            {
                if (routeSegment.NeighborElements(versionId).Contains(element2))
                    return true;
            }

            return false;
        }

        private static bool IsDifferentType(IRouteNetworkElement element1, IRouteNetworkElement element2, long versionId)
        {
            if (element1 is RouteNode routeNode)
            {
                if (element2 is RouteSegment)
                    return true;
            }

            if (element1 is RouteSegment routeSegment)
            {
                if (element2 is RouteNode)
                    return true;
            }

            return false;
        }

        private Result<List<IRouteNetworkElement>> LookupRouteNetworkObjects(RouteNetworkElementIdList routeNetworkElementIds, long versionId)
        {
            List<IRouteNetworkElement> result = new List<IRouteNetworkElement>();

            foreach (var networkElementId in routeNetworkElementIds)
            {
                var routeNetworkElement = _routeNetworkRepository.NetworkState.GetRouteNetworkElement(networkElementId, versionId);

                if (routeNetworkElement == null)
                    return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_CANNOT_FIND_ROUTE_NETWORK_ELEMENT, $"Cannot find any route network element with id: {networkElementId}"));
                else
                    result.Add(routeNetworkElement);
            }

            return Result.Ok<List<IRouteNetworkElement>>(result);
        }

        private static Result<List<RouteSegment>> SortRouteSegments(List<RouteSegment> routeNetworkElementsToBeSorted, long versionId)
        {
            if (routeNetworkElementsToBeSorted.Count == 1)
                return Result.Ok(routeNetworkElementsToBeSorted);

            var ends = FindLinkPathEnds(routeNetworkElementsToBeSorted, versionId);

            if (ends.Count < 1)
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SEGMENTS_ARE_NOT_ADJACENT, "No ends found in path. Make sure the links represent a path (not a trail or walk with repeating edges or vertices). Links: " + IdStringList(routeNetworkElementsToBeSorted)));

            if (ends.Count == 1)
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SEGMENTS_ARE_NOT_ADJACENT, "Only one end found in path. Make sure the links represent a path (not a trail or walk with repeating edges or vertices). Links: " + IdStringList(routeNetworkElementsToBeSorted)));

            if (ends.Count > 2)
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SEGMENTS_ARE_NOT_ADJACENT, ends.Count + " ends found in path. Make sure the links represent a connected path. Ends: " + IdStringList(ends)));

            List<RouteSegment> linksSorted = new List<RouteSegment>();
            List<RouteSegment> linksRemaning = new List<RouteSegment>();
            linksRemaning.AddRange(routeNetworkElementsToBeSorted);

            var currentSegment = ends[0];
            linksRemaning.Remove(currentSegment);

            while (currentSegment != null)
            {
                linksSorted.Add(currentSegment);

                var segmentToExamine = currentSegment;
                currentSegment = null;

                foreach (var neighborNode in segmentToExamine.NeighborElements(versionId))
                {
                    foreach (var neighborSegment in neighborNode.NeighborElements(versionId))
                    {
                        if (neighborSegment != segmentToExamine && linksRemaning.Contains(neighborSegment))
                        {
                            currentSegment = neighborSegment as RouteSegment;

                            if (currentSegment == null)
                                throw new ApplicationException($"Neighbors of route node element with id: {neighborNode.Id} was expected to be a route network segment.");

                            linksRemaning.Remove(currentSegment);
                        }
                    }
                }
            }

            if (linksSorted.Count != routeNetworkElementsToBeSorted.Count)
                return Result.Fail(new RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes.INVALID_WALK_SEGMENTS_ARE_NOT_ADJACENT, "Only " + linksSorted.Count + " out of " + routeNetworkElementsToBeSorted.Count + " could be sorted. Make sure the links represent a connected path. Route network segments that could not be sorted:" + IdStringList(routeNetworkElementsToBeSorted)));

            return Result.Ok(linksSorted);
        }

        public static List<RouteSegment> FindLinkPathEnds(List<RouteSegment> segments, long versionId)
        {
            List<RouteSegment> result = new List<RouteSegment>();

            foreach (var segment in segments)
            {
                // Check if we find no links (in the links list) related to the start node. If that's the case, it's an end
                bool segmentOutVertexIsAnEnd = true;

                foreach (var startSegment in segment.InV(versionId).NeighborElements(versionId))
                {
                    if (startSegment.Id != segment.Id && segments.Exists(s => s.Id == startSegment.Id))
                        segmentOutVertexIsAnEnd = false;
                }

                if (segmentOutVertexIsAnEnd)
                {
                    result.Add(segment);
                }


                // Check if we find no links (in the links list) related to the end node. If that's the case, it's an end
                bool segmentInVertexIsAnEnd = true;

                foreach (var endLink in segment.OutV(versionId).NeighborElements(versionId))
                {
                    if (endLink.Id != segment.Id && segments.Exists(s => s.Id == endLink.Id))
                        segmentInVertexIsAnEnd = false;
                }

                if (segmentInVertexIsAnEnd)
                {
                    result.Add(segment);
                }
            }

            return result;
        }

        private static string IdStringList(List<RouteSegment> segments)
        {
            string idStr = "";
            foreach (var segment in segments)
            {
                if (idStr.Length > 1)
                    idStr += ",";

                idStr += segment.Id;
            }

            return idStr;
        }

        private static RouteElementListSummary GetRouteNetworkElementsListSummary(List<IRouteNetworkElement> routeNetworkObjects)
        {
            if (routeNetworkObjects.Count == 0)
                return RouteElementListSummary.None;

            if (routeNetworkObjects.Count(o => o is IRouteNode) == routeNetworkObjects.Count)
                return RouteElementListSummary.RouteNodesOnly;

            if (routeNetworkObjects.Count(o => o is IRouteSegment) == routeNetworkObjects.Count)
                return RouteElementListSummary.RouteSegmentsOnly;

            if (routeNetworkObjects.Count(o => o is IRouteSegment) == routeNetworkObjects.Count)
                return RouteElementListSummary.RouteSegmentsOnly;

            return RouteElementListSummary.BothRouteNodesAndSegments;
        }


        private enum RouteElementListSummary
        {
            None,
            RouteNodesOnly,
            RouteSegmentsOnly,
            BothRouteNodesAndSegments
        }

    }
}
