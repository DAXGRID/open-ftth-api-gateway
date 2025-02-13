using FluentResults;
using OpenFTTH.Events.Core.Infos;
using OpenFTTH.EventSourcing;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.Util;
using OpenFTTH.UtilityGraphService.API.Commands;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.NodeContainers.Events;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.TerminalEquipments.Events;
using OpenFTTH.UtilityGraphService.Business.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFTTH.UtilityGraphService.Business.SpanEquipments
{
    /// <summary>
    /// The Span Equipment is used to model conduits and cables in the route network.
    /// Equipment that spans multiple route nodes and one or more route segments should be 
    /// modelled using the span equipment concept.
    /// </summary>
    public class SpanEquipmentAR : AggregateBase
    {
        private SpanEquipment? _spanEquipment;

        public SpanEquipmentAR()
        {
            Register<SpanEquipmentPlacedInRouteNetwork>(Apply);
            Register<SpanEquipmentAffixedToContainer>(Apply);
            Register<SpanEquipmentAffixSideChanged>(Apply);
            Register<SpanSegmentsCut>(Apply);
            Register<SpanEquipmentCutReverted>(Apply);
            Register<SpanSegmentsConnectedToSimpleTerminals>(Apply);
            Register<SpanSegmentDisconnectedFromTerminal>(Apply);
            Register<SpanSegmentsDisconnectedFromTerminals>(Apply);
            Register<SpanEquipmentDetachedFromContainer>(Apply);
            Register<SpanEquipmentDetachedFromParent>(Apply);
            Register<AdditionalStructuresAddedToSpanEquipment>(Apply);
            Register<SpanStructureRemoved>(Apply);
            Register<SpanEquipmentRemoved>(Apply);
            Register<SpanEquipmentMoved>(Apply);
            Register<SpanEquipmentMerged>(Apply);
            Register<SpanEquipmentMarkingInfoChanged>(Apply);
            Register<SpanEquipmentAddressInfoChanged>(Apply);
            Register<SpanEquipmentManufacturerChanged>(Apply);
            Register<SpanEquipmentSpecificationChanged>(Apply);
            Register<SpanEquipmentAffixedToParent>(Apply);
            Register<SpanEquipmentNamingInfoChanged>(Apply);
        }

        #region Place Span Equipment
        public Result PlaceSpanEquipmentInRouteNetwork(
            CommandContext cmdContext,
            IReadOnlyDictionary<Guid, SpanEquipment> spanEquipments,
            LookupCollection<SpanEquipmentSpecification> spanEquipmentSpecifications,
            Guid spanEquipmentId, 
            Guid spanEquipmentSpecificationId,
            RouteNetworkInterest interest,
            Guid? manufacturerId,
            NamingInfo? namingInfo,
            LifecycleInfo? lifecycleInfo,
            MarkingInfo? markingInfo,
            AddressInfo? addressInfo)
        {
            this.Id = spanEquipmentId;

            if (spanEquipmentId == Guid.Empty)
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_ID_CANNOT_BE_EMPTY, "Span equipment id cannot be empty. A unique id must be provided by client."));

            if (spanEquipments.ContainsKey(spanEquipmentId))
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_ALREADY_EXISTS, $"A span equipment with id: {spanEquipmentId} already exists."));

            if (interest.Kind != RouteNetworkInterestKindEnum.WalkOfInterest)
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_INTEREST_KIND_MUST_BE_WALK_OF_INTEREST, "Interest kind must be WalkOfInterest."));

            if (!spanEquipmentSpecifications.ContainsKey(spanEquipmentSpecificationId))
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_SPECIFICATION_ID_NOT_FOUND, $"Cannot find span equipment specification with id: {spanEquipmentSpecificationId}"));

            var spanEquipment = CreateSpanEquipmentFromSpecification(
                spanEquipmentId: spanEquipmentId, 
                specification: spanEquipmentSpecifications[spanEquipmentSpecificationId], 
                walkOfInterestId: interest.Id, 
                nodesOfInterestIds: new Guid[] { interest.RouteNetworkElementRefs.First(), interest.RouteNetworkElementRefs.Last() }, 
                manufacturerId: manufacturerId, 
                namingInfo: namingInfo, 
                lifecycleInfo: lifecycleInfo,
                markingInfo: markingInfo,
                addressInfo: addressInfo,
                utilityNetworkHops: null,
                isCable: spanEquipmentSpecifications[spanEquipmentSpecificationId].IsCable
             );

            RaiseEvent(
                new SpanEquipmentPlacedInRouteNetwork(spanEquipment)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );

            return Result.Ok();
        }

      

        public Result PlaceSpanEquipmentInUtilityNetwork(
            CommandContext cmdContext,
            IReadOnlyDictionary<Guid,SpanEquipment> spanEquipments, 
            LookupCollection<SpanEquipmentSpecification> spanEquipmentSpecifications, 
            Guid spanEquipmentId, Guid spanEquipmentSpecificationId,
            Guid walkOfInterestId,
            RouteNetworkElementIdList walk,
            UtilityNetworkHop[] utilityNetworkHops, 
            Guid? manufacturerId, 
            NamingInfo? namingInfo, LifecycleInfo? lifecycleInfo, 
            MarkingInfo? markingInfo, 
            AddressInfo? addressInfo)
        {
            this.Id = spanEquipmentId;

            if (walk.Count() < 3)
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_WALK_MUST_CONTAIN_AT_LEAST_THREE_ELEMENTS, "The provided walk must contain at least threee element"));

            if (spanEquipmentId == Guid.Empty)
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_ID_CANNOT_BE_EMPTY, "Span equipment id cannot be empty. A unique id must be provided by client."));

            if (spanEquipments.ContainsKey(spanEquipmentId))
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_ALREADY_EXISTS, $"A span equipment with id: {spanEquipmentId} already exists."));

            if (!spanEquipmentSpecifications.ContainsKey(spanEquipmentSpecificationId))
                return Result.Fail(new PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes.INVALID_SPAN_EQUIPMENT_SPECIFICATION_ID_NOT_FOUND, $"Cannot find span equipment specification with id: {spanEquipmentSpecificationId}"));

            var spanEquipment = CreateSpanEquipmentFromSpecification(
                spanEquipmentId: spanEquipmentId,
                specification: spanEquipmentSpecifications[spanEquipmentSpecificationId],
                walkOfInterestId: walkOfInterestId,
                nodesOfInterestIds: new Guid[] { walk.First(), walk.Last() },
                utilityNetworkHops: utilityNetworkHops,
                manufacturerId: manufacturerId,
                namingInfo: namingInfo,
                lifecycleInfo: lifecycleInfo,
                markingInfo: markingInfo,
                addressInfo: addressInfo,
                isCable: spanEquipmentSpecifications[spanEquipmentSpecificationId].IsCable
             );

            RaiseEvent(
                new SpanEquipmentPlacedInRouteNetwork(spanEquipment)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );

            return Result.Ok();
        }

   
        private IEnumerable<Guid> GetReversedIds(RouteNetworkElementIdList routeNetworkElementRefs)
        {
            List<Guid> reversed = new();

            for (int i = routeNetworkElementRefs.Count - 1; i >= 0; i--)
            {
                reversed.Add(routeNetworkElementRefs[i]);
            }

            return reversed;
        }

        private HashSet<UtilityNetworkHop> FindUtilityNetworkHopsReferencingSpanEquipment(SpanEquipment spanEquipment)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            HashSet<UtilityNetworkHop> utilityNetworkHops = new();

            // Create hash set with span segment ids for quick lookup
            HashSet<Guid> spanSegmentIds = new();

            foreach (var spanStructure in spanEquipment.SpanStructures)
            {
                foreach (var spanSegment in spanStructure.SpanSegments)
                {
                    spanSegmentIds.Add(spanSegment.Id);
                }
            }

            // Find all utility network hops that has a reference to the any segment in the span equipment
            if (_spanEquipment.UtilityNetworkHops != null)
            {
                foreach (var utiliyNetworkHop in _spanEquipment.UtilityNetworkHops)
                {
                    foreach (var affix in utiliyNetworkHop.ParentAffixes)
                    {
                        if (spanSegmentIds.Contains(affix.SpanSegmentId))
                            utilityNetworkHops.Add(utiliyNetworkHop);
                    }
                }
            }

            return utilityNetworkHops;
        }


        private Dictionary<UtilityNetworkHop, List<Guid>> FindUtilityNetworkHopSegmentsReferencingSpanEquipment(SpanEquipment spanEquipment)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            Dictionary<UtilityNetworkHop, List<Guid>> utilityNetworkHops = new();

            // Create hash set with span segment ids for quick lookup
            HashSet<Guid> spanSegmentIds = new();

            foreach (var spanStructure in spanEquipment.SpanStructures)
            {
                foreach (var spanSegment in spanStructure.SpanSegments)
                {
                    spanSegmentIds.Add(spanSegment.Id);
                }
            }

            // Find all utility network hops that has a reference to the any segment in the span equipment
            if (_spanEquipment.UtilityNetworkHops != null)
            {
                foreach (var utiliyNetworkHop in _spanEquipment.UtilityNetworkHops)
                {
                    foreach (var affix in utiliyNetworkHop.ParentAffixes)
                    {
                        if (spanSegmentIds.Contains(affix.SpanSegmentId))
                        {
                            if (utilityNetworkHops.ContainsKey(utiliyNetworkHop))
                            {
                                utilityNetworkHops[utiliyNetworkHop].Add(affix.SpanSegmentId);
                            }
                            else
                            {
                                utilityNetworkHops.Add(utiliyNetworkHop, new List<Guid> { affix.SpanSegmentId });
                            }
                        }
                    }
                }
            }

            return utilityNetworkHops;
        }


        private static SpanEquipment CreateSpanEquipmentFromSpecification(Guid spanEquipmentId, SpanEquipmentSpecification specification, Guid walkOfInterestId, Guid[] nodesOfInterestIds, Guid? manufacturerId, NamingInfo? namingInfo, LifecycleInfo? lifecycleInfo, MarkingInfo? markingInfo, AddressInfo? addressInfo, UtilityNetworkHop[]? utilityNetworkHops, bool isCable)
        {
            List<SpanStructure> spanStructuresToInclude = new List<SpanStructure>();

            // Create root structure
            spanStructuresToInclude.Add(
                new SpanStructure(
                    id: Guid.NewGuid(),
                    specificationId: specification.RootTemplate.SpanStructureSpecificationId,
                    level: 1,
                    parentPosition: 0,
                    position: 1,
                    spanSegments: new SpanSegment[] { new SpanSegment(Guid.NewGuid(), 0, 1) }
                )
            );

            // Add level 2 structures
            foreach (var template in specification.RootTemplate.GetAllSpanStructureTemplatesRecursive().Where(t => t.Level == 2))
            {
                spanStructuresToInclude.Add(
                    new SpanStructure(
                        id: Guid.NewGuid(),
                        specificationId: template.SpanStructureSpecificationId,
                        level: template.Level,
                        parentPosition: 1,
                        position: template.Position,
                        spanSegments: new SpanSegment[] { new SpanSegment(Guid.NewGuid(), 0, 1) }
                    )
                );
            }

            var spanEquipment = new SpanEquipment(spanEquipmentId, specification.Id, walkOfInterestId, nodesOfInterestIds, spanStructuresToInclude.ToArray())
            {
                ManufacturerId = manufacturerId == null ? Guid.Empty : manufacturerId,
                NamingInfo = namingInfo,
                LifecycleInfo = lifecycleInfo,
                MarkingInfo = markingInfo,
                AddressInfo = addressInfo,
                UtilityNetworkHops = utilityNetworkHops,
                IsCable = isCable
            };

            return spanEquipment;
        }

        private void Apply(SpanEquipmentPlacedInRouteNetwork @event)
        {
            _spanEquipment = @event.Equipment;
        }
        #endregion

        #region Affix parents
        public Result<ValidatedRouteNetworkWalk> AffixToParent(CommandContext cmdContext, ValidatedRouteNetworkWalk childWalkOfInterest, UtilityNetworkHop utilityNetworkHop, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            var routeNetworkHops = FindRouteNetworkHops(childWalkOfInterest);

            if (CheckIfNewHopHasValidStartAndEndInsideInWalkOfInterest(childWalkOfInterest, routeNetworkHops, utilityNetworkHop))
            {
                var reversedUtilityNetworkHop = ReverseHopIfNeeded(childWalkOfInterest, utilityNetworkHop);
                var reversedUtilityNetworkHopWalkOfInterest = ReverseWalkOfInterestNeeded(childWalkOfInterest, utilityNetworkHopWalkOfInterest, utilityNetworkHop);

                var existingHopToUse = FindExistingRouteNetworkHopThatCanContainNewUtilityNetworkHop(routeNetworkHops, reversedUtilityNetworkHop);

                var newUtilityNetworkHopList = CreateNewUtilityNetworkHopList(childWalkOfInterest, routeNetworkHops, reversedUtilityNetworkHop, existingHopToUse.SequenceNumber);

                var newWalkOfInterest = CreateNewWalkOfInterest(childWalkOfInterest, reversedUtilityNetworkHopWalkOfInterest, existingHopToUse);


                RaiseEvent(
                    new SpanEquipmentAffixedToParent(this.Id, newUtilityNetworkHopList)
                    {
                        CorrelationId = cmdContext.CorrelationId,
                        IncitingCmdId = cmdContext.CmdId,
                        UserName = cmdContext.UserContext?.UserName,
                        WorkTaskId = cmdContext.UserContext?.WorkTaskId
                    }
                );

                return Result.Ok(newWalkOfInterest);
            }
            else if (CheckIfNewHopIsExtendingExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                var reversedUtilityNetworkHop = ExtentReverseHopIfNeeded(childWalkOfInterest, utilityNetworkHopWalkOfInterest, utilityNetworkHop);

                var reversedUtilityNetworkHopWalkOfInterest = ExtentReverseWalkOfInterestIfNeeded(childWalkOfInterest, utilityNetworkHopWalkOfInterest);

                var newWalkOfInterest = ExtentCreateNewWalkOfInterestIfNeeded(childWalkOfInterest, reversedUtilityNetworkHopWalkOfInterest, reversedUtilityNetworkHop);

                var newUtilityNetworkHopList = ExtentCreateNewUtilityNetworkHopList(childWalkOfInterest, reversedUtilityNetworkHopWalkOfInterest, reversedUtilityNetworkHop);

                RaiseEvent(
                    new SpanEquipmentAffixedToParent(this.Id, newUtilityNetworkHopList)
                    {
                        CorrelationId = cmdContext.CorrelationId,
                        IncitingCmdId = cmdContext.CmdId,
                        UserName = cmdContext.UserContext?.UserName,
                        WorkTaskId = cmdContext.UserContext?.WorkTaskId
                    }
                );


                return Result.Ok(newWalkOfInterest);

            }
            else
            {
                return Result.Fail(new AffixSpanEquipmentToParentError(AffixSpanEquipmentToParentErrorCodes.NEW_UTILITY_NETWORK_HOP_IS_NOT_VALID, $"The hop from node: {utilityNetworkHop.FromNodeId} to node: {utilityNetworkHop} is not valid in regard to span equipment with id: {this.Id} and walk of interest with id: {_spanEquipment.WalkOfInterestId}"));
            }
        }

        private UtilityNetworkHop[] ExtentCreateNewUtilityNetworkHopList(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest, UtilityNetworkHop newUtilityNetworkHop)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            List<UtilityNetworkHop> result = new();

            if (CheckIfNewHopExtentsStartOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                result.Add(newUtilityNetworkHop);

                if (_spanEquipment.UtilityNetworkHops != null)
                    result.AddRange(_spanEquipment.UtilityNetworkHops);
            }
            else if (CheckIfNewHopExtentsEndOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                if (_spanEquipment.UtilityNetworkHops != null)
                    result.AddRange(_spanEquipment.UtilityNetworkHops);

                result.Add(newUtilityNetworkHop);
            }
            else
            {
                throw new ApplicationException($"Error creating new utility hop list while affixing {this.Id} to {newUtilityNetworkHop} Expected new hop to either start or end in existing walk of interest.");
            }

            return result.ToArray();
        }

        private ValidatedRouteNetworkWalk ExtentCreateNewWalkOfInterestIfNeeded(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest, UtilityNetworkHop newUtilityNetworkHop)
        {
            List<Guid> newWalkIds = new();

            if (CheckIfNewHopExtentsStartOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                // Add hop walk ids
                newWalkIds.AddRange(utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs);

                for (int i = 1; i < childWalkOfInterest.RouteNetworkElementRefs.Count; i++)
                    newWalkIds.Add(childWalkOfInterest.RouteNetworkElementRefs[i]);
            }
            else if (CheckIfNewHopExtentsEndOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                // Add child walk ids
                newWalkIds.AddRange(childWalkOfInterest.RouteNetworkElementRefs);

                for (int i = 1; i < utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs.Count; i++)
                    newWalkIds.Add(utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs[i]);
            }
            else
            {
                throw new ApplicationException($"Error creating new walk while affixing {this.Id} to {newUtilityNetworkHop} Expected new hop to either start or end in existing walk of interest.");
            }

            RouteNetworkElementIdList idList = new RouteNetworkElementIdList();
            idList.AddRange(newWalkIds);

            return new ValidatedRouteNetworkWalk(idList);
        }

        private bool CheckIfNewHopIsExtendingExistingWalkOfInterest(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest)
        {
            if (CheckIfNewHopExtentsStartOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
                return true;

            if (CheckIfNewHopExtentsEndOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
                return true;

            return false;
        }

        private bool CheckIfNewHopExtentsStartOfExistingWalkOfInterest(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest)
        {
            if (childWalkOfInterest.FromNodeId == utilityNetworkHopWalkOfInterest.FromNodeId && childWalkOfInterest.ToNodeId != utilityNetworkHopWalkOfInterest.ToNodeId)
                return true;
            if (childWalkOfInterest.FromNodeId == utilityNetworkHopWalkOfInterest.ToNodeId && childWalkOfInterest.ToNodeId != utilityNetworkHopWalkOfInterest.FromNodeId)
                return true;

            return false;
        }

        private bool CheckIfNewHopExtentsEndOfExistingWalkOfInterest(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest)
        {
            if (childWalkOfInterest.ToNodeId == utilityNetworkHopWalkOfInterest.FromNodeId && childWalkOfInterest.FromNodeId != utilityNetworkHopWalkOfInterest.ToNodeId)
                return true;
            if (childWalkOfInterest.ToNodeId == utilityNetworkHopWalkOfInterest.ToNodeId && childWalkOfInterest.FromNodeId != utilityNetworkHopWalkOfInterest.FromNodeId)
                return true;

            return false;
        }

        private ValidatedRouteNetworkWalk ReverseWalkOfInterestNeeded(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest, UtilityNetworkHop utilityNetworkHop)
        {
            bool reverse = false;

            foreach (var nodeId in childWalkOfInterest.NodeIds)
            {
                if (utilityNetworkHop.FromNodeId == nodeId)
                {
                    break;
                }

                if (utilityNetworkHop.ToNodeId == nodeId)
                {
                    reverse = true;
                }
            }

            if (reverse)
            {
                utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs.Reverse();
            }
            
            return new ValidatedRouteNetworkWalk(utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs);
        }

        private ValidatedRouteNetworkWalk ExtentReverseWalkOfInterestIfNeeded(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest)
        {
            bool reverse = false;

            if (CheckIfNewHopExtentsStartOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                if (utilityNetworkHopWalkOfInterest.ToNodeId != childWalkOfInterest.FromNodeId)
                  reverse = true;
            }

            if (CheckIfNewHopExtentsEndOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                if (utilityNetworkHopWalkOfInterest.FromNodeId != childWalkOfInterest.ToNodeId)
                    reverse = true;
            }

            if (reverse)
            {
                utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs.Reverse();
            }

            return new ValidatedRouteNetworkWalk(utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs);
        }

        private ValidatedRouteNetworkWalk CreateNewWalkOfInterest(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest, ExistingRouteHop routeNetworkHop)
        {
            List<Guid> newWalkIds = new();

            // Add eventually walk ids of previous hops
            for (int i = 0; i < routeNetworkHop.FromWalkOfInterestIndex; i++)
                newWalkIds.Add(childWalkOfInterest.RouteNetworkElementRefs[i]);

            // Add evt. gab before new hop begins
            for (int i = routeNetworkHop.FromWalkOfInterestIndex; i < routeNetworkHop.ToWalkOfInterestIndex; i++)
            {
                // As soon we hit from node of new utility hop, stop adding route network elements from existing woi
                if (childWalkOfInterest.RouteNetworkElementRefs[i] == utilityNetworkHopWalkOfInterest.FromNodeId)
                    break;

                newWalkIds.Add(childWalkOfInterest.RouteNetworkElementRefs[i]);
            }

            // Add hop walk ids
            newWalkIds.AddRange(utilityNetworkHopWalkOfInterest.RouteNetworkElementRefs);

            // Add evt. gab efter new hop
            bool toNodeFound = false;

            for (int i = routeNetworkHop.FromWalkOfInterestIndex; i <= routeNetworkHop.ToWalkOfInterestIndex; i++)
            {
                if (toNodeFound)
                    newWalkIds.Add(childWalkOfInterest.RouteNetworkElementRefs[i]);

                // As soon we hit to node of new utility hop, we start adding route network elements from existing woi
                if (childWalkOfInterest.RouteNetworkElementRefs[i] == utilityNetworkHopWalkOfInterest.ToNodeId)
                    toNodeFound = true;
            }


            // Add eventually walk ids after hop
            for (int i = routeNetworkHop.ToWalkOfInterestIndex + 1; i < childWalkOfInterest.RouteNetworkElementRefs.Count; i++)
                newWalkIds.Add(childWalkOfInterest.RouteNetworkElementRefs[i]);

            RouteNetworkElementIdList idList = new RouteNetworkElementIdList();
            idList.AddRange(newWalkIds);

            return new ValidatedRouteNetworkWalk(idList);
        }

        private ValidatedRouteNetworkWalk CreateNewWalkOfInterest(List<ExistingRouteHop> routeHops)
        {
            List<Guid> newWalkIds = new();

            bool first = true;

            foreach (var routeHop in routeHops)
            {
                if (first)
                {
                    newWalkIds.AddRange(routeHop.Walk.RouteNetworkElementRefs);
                    first = false;
                }
                else
                {
                    for (int i = 1; i < routeHop.Walk.RouteNetworkElementRefs.Count; i++)
                        newWalkIds.Add(routeHop.Walk.RouteNetworkElementRefs[i]);
                }

               
            }


            RouteNetworkElementIdList idList = new RouteNetworkElementIdList();
            idList.AddRange(newWalkIds);

            return new ValidatedRouteNetworkWalk(idList);
        }


        private UtilityNetworkHop[] CreateNewUtilityNetworkHopList(ValidatedRouteNetworkWalk walkOfInterest, List<ExistingRouteHop> routeHops, UtilityNetworkHop newUtilityNetworkHop, int newUtilityHopIndex)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            List<UtilityNetworkHop> result = new();

            // If first utility hop just create new list
            if (_spanEquipment.UtilityNetworkHops == null || _spanEquipment.UtilityNetworkHops.Length == 0)
                return new UtilityNetworkHop[] { newUtilityNetworkHop };

            bool newHopAdded = false;

            foreach (var routeHop in routeHops.Where(rh => rh.IsUtilityHop))
            {
                if (newUtilityHopIndex < routeHop.SequenceNumber)
                {
                    if (!newHopAdded)
                    {
                        result.Add(newUtilityNetworkHop);

                        newHopAdded = true;
                    }
                }

                result.Add(routeHop.UtilityNetworkHop);
            }

            if (!newHopAdded)
                result.Add(newUtilityNetworkHop);

            return result.ToArray();
        }

        private bool CheckIfNewHopHasValidStartAndEndInsideInWalkOfInterest(ValidatedRouteNetworkWalk walkOfInterest, List<ExistingRouteHop> routeHops, UtilityNetworkHop utilityNetworkHop)
        {
            if (FindExistingRouteNetworkHopThatCanContainNewUtilityNetworkHop(routeHops, utilityNetworkHop) != null)
                return true;
            else
                return false;
        }

        private ExistingRouteHop? FindExistingRouteNetworkHopThatCanContainNewUtilityNetworkHop(List<ExistingRouteHop> routeHops, UtilityNetworkHop utilityNetworkHop)
        {
            foreach (var hop in routeHops.Where(rh => rh.IsUtilityHop == false))
            {
                bool fromFound = false;
                bool toFound = false;

                foreach (var nodeId in hop.Walk.NodeIds)
                {
                    if (utilityNetworkHop.FromNodeId == nodeId)
                        fromFound = true;

                    else if (utilityNetworkHop.ToNodeId == nodeId)
                        toFound = true;
                }

                if (fromFound && toFound)
                    return hop;
            }

            return null;

        }

    
        private UtilityNetworkHop ReverseHopIfNeeded(ValidatedRouteNetworkWalk walkOfInterest, UtilityNetworkHop utilityNetworkHop)
        {
            foreach (var nodeId in walkOfInterest.NodeIds)
            {
                if (utilityNetworkHop.FromNodeId == nodeId)
                {
                    return utilityNetworkHop;
                }

                if (utilityNetworkHop.ToNodeId == nodeId)
                {
                    return utilityNetworkHop.Reverse();
                }
            }

            return utilityNetworkHop;
        }

        private UtilityNetworkHop ExtentReverseHopIfNeeded(ValidatedRouteNetworkWalk childWalkOfInterest, ValidatedRouteNetworkWalk utilityNetworkHopWalkOfInterest, UtilityNetworkHop utilityNetworkHop)
        {
            bool reverse = false;

            if (CheckIfNewHopExtentsStartOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                if (utilityNetworkHopWalkOfInterest.ToNodeId != childWalkOfInterest.FromNodeId)
                    reverse = true;
            }

            if (CheckIfNewHopExtentsEndOfExistingWalkOfInterest(childWalkOfInterest, utilityNetworkHopWalkOfInterest))
            {
                if (utilityNetworkHopWalkOfInterest.FromNodeId != childWalkOfInterest.ToNodeId)
                    reverse = true;
            }

            if (reverse)
                return utilityNetworkHop.Reverse();
            else
                return utilityNetworkHop;
        }

        private void Apply(SpanEquipmentAffixedToParent @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Affix To Node Container
        public Result AffixToNodeContainer(
            CommandContext cmdContext,
            IReadOnlyDictionary<Guid,NodeContainer> nodeContainers,
            RouteNetworkInterest spanEquipmentInterest,
            Guid nodeContainerRouteNodeId,
            Guid nodeContainerId,
            Guid spanSegmentId,
            NodeContainerSideEnum nodeContainerIngoingSide)
        {
            if (!spanEquipmentInterest.RouteNetworkElementRefs.Contains(nodeContainerRouteNodeId))
            {
                return Result.Fail(new AffixSpanEquipmentToNodeContainerError(
                        AffixSpanEquipmentToNodeContainerErrorCodes.SPAN_EQUIPMENT_AND_NODE_CONTAINER_IS_NOT_COLOCATED,
                        $"The walk of span equipment with id: {this.Id} do not include the route network element with id: {nodeContainerRouteNodeId} where the node container with id: {nodeContainerId} is located.")
                    );
            }

            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (IsAlreadyAffixedToNodeContainerInRouteNode(nodeContainers, nodeContainerRouteNodeId))
            {
                if (SideHasChanged(nodeContainers, nodeContainerRouteNodeId, nodeContainerIngoingSide))
                {
                    RaiseEvent(
                        new SpanEquipmentAffixSideChanged(this.Id, nodeContainerId, nodeContainerIngoingSide)
                        {
                            CorrelationId = cmdContext.CorrelationId,
                            IncitingCmdId = cmdContext.CmdId,
                            UserName = cmdContext.UserContext?.UserName,
                            WorkTaskId = cmdContext.UserContext?.WorkTaskId
                        }
                    );
                }
                else
                {
                    return Result.Fail(new AffixSpanEquipmentToNodeContainerError(
                            AffixSpanEquipmentToNodeContainerErrorCodes.SPAN_EQUIPMENT_ALREADY_AFFIXED_TO_NODE_CONTAINER,
                            $"The span equipment: {this.Id} is already affixed to side: {nodeContainerIngoingSide.ToString()} of node container: {nodeContainerId} in route node: {nodeContainerRouteNodeId}")
                        );
                }
            }
            else
            {

                var affix = new SpanEquipmentNodeContainerAffix(
                    routeNodeId: nodeContainerRouteNodeId,
                    nodeContainerId: nodeContainerId,
                    nodeContainerIngoingSide: nodeContainerIngoingSide
                );

                RaiseEvent(
                    new SpanEquipmentAffixedToContainer(this.Id, affix)
                    {
                        CorrelationId = cmdContext.CorrelationId,
                        IncitingCmdId = cmdContext.CmdId,
                        UserName = cmdContext.UserContext?.UserName,
                        WorkTaskId = cmdContext.UserContext?.WorkTaskId
                    }
                );
            }

            return Result.Ok();
        }

        private bool IsAlreadyAffixedToNodeContainerInRouteNode(IReadOnlyDictionary<Guid,NodeContainer> nodeContainers, Guid nodeContainerRouteNodeId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.NodeContainerAffixes == null)
                return false;

            foreach (var affix in _spanEquipment.NodeContainerAffixes)
            {
                if (nodeContainers[affix.NodeContainerId].RouteNodeId == nodeContainerRouteNodeId)
                    return true;
            }

            return false;
        }

        private bool SideHasChanged(IReadOnlyDictionary<Guid,NodeContainer> nodeContainers, Guid nodeContainerRouteNodeId, NodeContainerSideEnum nodeContainerIngoingSide)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.NodeContainerAffixes == null)
                throw new ApplicationException($"Invalid internal state. NodeContainerAffixes property cannot be null. This method can only be called on span equipment that is already affixed to a node container side.");

            foreach (var affix in _spanEquipment.NodeContainerAffixes)
            {
                if (nodeContainers[affix.NodeContainerId].RouteNodeId == nodeContainerRouteNodeId)
                {
                    if (affix.NodeContainerIngoingSide != nodeContainerIngoingSide)
                        return true;
                }
            }

            return false;
        }

        private void Apply(SpanEquipmentAffixedToContainer @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        private void Apply(SpanEquipmentAffixSideChanged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Detach From Node Container
        public Result DetachFromNodeContainer(CommandContext cmdContext, NodeContainer nodeContainer)
        {
            if (!IsAffixedToNodeContainer(nodeContainer.Id))
            {
                return Result.Fail(new DetachSpanEquipmentFromNodeContainerError(
                        DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_EQUIPMENT_IS_NOT_AFFIXED_TO_NODE_CONTAINER,
                        $"The span equipment with id: {this.Id} is not affixed to the node container: {nodeContainer.Id}")
                    );
            }

            var canBeDetachedResult = CanBeDetached(nodeContainer);

            if (canBeDetachedResult.IsFailed)
                return canBeDetachedResult;

            RaiseEvent(
                new SpanEquipmentDetachedFromContainer(this.Id, nodeContainer.Id)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );

            return Result.Ok();
        }

        private void Apply(SpanEquipmentDetachedFromContainer @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        private Result CanBeDetached(NodeContainer nodeContainer)
        {
            HashSet<Guid> spanSegmentsCutValidatedOk = new HashSet<Guid>();

            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // Check all span segments that are connected to/from the route node of the node container
            foreach (var structure in _spanEquipment.SpanStructures)
            {
                foreach (var segment in structure.SpanSegments)
                {
                    if (_spanEquipment.NodesOfInterestIds[segment.FromNodeOfInterestIndex] == nodeContainer.RouteNodeId)
                    {
                        if (segment.FromTerminalId != Guid.Empty)
                        {
                            return Result.Fail(new DetachSpanEquipmentFromNodeContainerError(
                                DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_SEGMENT_IS_CONNECTED_INSIDE_NODE_CONTAINER,
                                $"The span segment with id: {segment.Id} is connected inside the node container with id: {nodeContainer.Id} Cannot detach a span equipment from a node container if any connected span segments.")
                            );
                        }

                        // If span equipment is passing through node container, the span segment must be cut, which is not allowed either
                        if (nodeContainer.RouteNodeId != _spanEquipment.NodesOfInterestIds.First() && nodeContainer.RouteNodeId != _spanEquipment.NodesOfInterestIds.Last())
                        {
                            return Result.Fail(new DetachSpanEquipmentFromNodeContainerError(
                                DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_SEGMENT_IS_CUT_INSIDE_NODE_CONTAINER,
                                $"The span segment with id: {segment.Id} is cut inside the node container with id: {nodeContainer.Id} Cannot detach a span equipment from a node container if any span segments are cut.")
                            );
                        }
                    }

                    if (_spanEquipment.NodesOfInterestIds[segment.ToNodeOfInterestIndex] == nodeContainer.RouteNodeId)
                    {
                        if (segment.ToTerminalId != Guid.Empty)
                        {
                            return Result.Fail(new DetachSpanEquipmentFromNodeContainerError(
                                DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_SEGMENT_IS_CONNECTED_INSIDE_NODE_CONTAINER,
                                $"The span segment with id: {segment.Id} is connected inside the node container with id: {nodeContainer.Id} Cannot detach a span equipment from a node container if any connected span segments.")
                            );
                        }

                        // If span equipment is passing through node container, the span segment must be cut, which is not allowed either
                        if (nodeContainer.RouteNodeId != _spanEquipment.NodesOfInterestIds.First() && nodeContainer.RouteNodeId != _spanEquipment.NodesOfInterestIds.Last())
                        {
                            return Result.Fail(new DetachSpanEquipmentFromNodeContainerError(
                                DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_SEGMENT_IS_CUT_INSIDE_NODE_CONTAINER,
                                $"The span segment with id: {segment.Id} is cut inside the node container with id: {nodeContainer.Id} Cannot detach a span equipment from a node container if any span segments are cut.")
                            );
                        }
                    }

                }
            }

            return Result.Ok();
        }

        #endregion

        #region Detach From Parents In Node

        public Result DetachFromParentsInNode(CommandContext cmdContext, Guid routeNodeId, ValidatedRouteNetworkWalk existingWalk)
        {
            if (!IsAffixedInRouteNode(routeNodeId, existingWalk))
            {
                return Result.Fail(new DetachSpanEquipmentFromNodeContainerError(
                        DetachSpanEquipmentFromNodeContainerErrorCodes.SPAN_EQUIPMENT_IS_NOT_AFFIXED_TO_PARENTS_IN_NODE,
                        $"The span equipment with id: {this.Id} has no relation to any parent segments in route node with id: {routeNodeId}")
                    );
            }

            var newUtilityNetworkHopList = DetachCreateNewUtilityNetworkHopList(routeNodeId, existingWalk);

            RaiseEvent(
                new SpanEquipmentDetachedFromParent(this.Id, newUtilityNetworkHopList)
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                }
            );


            return Result.Ok();

        }

        private UtilityNetworkHop[] DetachCreateNewUtilityNetworkHopList(Guid routeNodeId, ValidatedRouteNetworkWalk walk)
        {
            if(_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.UtilityNetworkHops == null)
                throw new ApplicationException($"Invalid internal state. UtilityNetworkHops cannot be null. This function should never be called on span equipmens with no utility network hops.");

            List<UtilityNetworkHop> newUtilityNetworkHopList = new();

            var hops = FindRouteNetworkHops(walk);

            foreach (var hop in hops.Where(h => h.IsUtilityHop))
            {
                if (!hop.Walk.RouteNetworkElementRefs.Contains(routeNodeId))
                    newUtilityNetworkHopList.Add(hop.UtilityNetworkHop);
            }

            return newUtilityNetworkHopList.ToArray();
        }

        private bool IsAffixedInRouteNode(Guid routeNodeId, ValidatedRouteNetworkWalk walk)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.UtilityNetworkHops == null || _spanEquipment.UtilityNetworkHops.Length == 0)
                return false;

            var hops = FindRouteNetworkHops(walk);

            foreach (var hop in hops.Where(h => h.IsUtilityHop))
            {
                if (hop.Walk.RouteNetworkElementRefs.Contains(routeNodeId))
                    return true;
            }

            return false;
        }


        private void Apply(SpanEquipmentDetachedFromParent @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }


        #endregion

        #region Cut Span Segments
        public Result CutSpanSegments(
            CommandContext cmdContext,
            RouteNetworkInterest spanEquipmentWalkOfInterest, 
            Guid routeNodeId, 
            Guid[] spanSegmentsToCut)
        {
            if (routeNodeId == Guid.Empty)
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.INVALID_ROUTE_NODE_ID_CANNOT_BE_EMPTY, "Route node id cannot be empty."));

            if (spanSegmentsToCut.Length == 0)
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_LIST_CANNOT_BE_EMPTY, "A list of span segments to cut must be provided."));

            if (!spanEquipmentWalkOfInterest.RouteNetworkElementRefs.Contains(routeNodeId))
            {
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                    CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_EQUIPMENT_AND_ROUTE_NODE_WHERE_TO_CUT_IS_NOT_COLOCATED,
                    $"The walk of span equipment with id: {this.Id} do not include the route network element with id: {routeNodeId} where to cut.")
                );
            }

            if (spanEquipmentWalkOfInterest.RouteNetworkElementRefs.First() == routeNodeId || spanEquipmentWalkOfInterest.RouteNetworkElementRefs.Last() == routeNodeId)
            {
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                    CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_EQUIPMENT_CANNOT_BE_CUT_AT_ENDS,
                    $"The route network node: {routeNodeId} is located at one of the ends of span equipment: {this.Id} This makes no sense. You cannot cut a span equipment at its ends.")
                );
            }

            // Chat that span equipment is affixed to container at node where the cuts are
            if (!IsAffixedToNodeContainer(routeNodeId))
            {
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                   CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_EQUIPMENT_NOT_AFFIXED_TO_NODE_CONTAINER,
                   $"Cutting span segments is only allowed if the span equipment: {this.Id} is affixed to a node container in route node: {routeNodeId}")
               );
            }

            // Check that cuts are valid
            var spanSegmentToCutHash = spanSegmentsToCut.ToHashSet();
            
            var validCutsResult = IsCutsValid(routeNodeId, spanSegmentToCutHash);
            
            if (validCutsResult.IsFailed)
                return validCutsResult;

            if (IsOuterSpanMissingToBeCut(routeNodeId, spanSegmentToCutHash))
            {
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                   CutSpanSegmentsAtRouteNodeErrorCodes.OUTER_SPAN_IS_NOT_CUT,
                   $"Cutting inner spans without cutting the outer span as well is not allowed. The outer span of span equipment with id: {this.Id} is currently not cut at route node with id: {routeNodeId}. Neither is the outer span specified to cut as part of the command.")
               );
            }

            // If we get to here, then everything should be in perfect order

            var @event = new SpanSegmentsCut(
                spanEquipmentId: this.Id,
                cutNodeOfInterestId: routeNodeId,
                cutNodeOfInterestIndex: GetCutNodeOfInterestIndex(routeNodeId, spanEquipmentWalkOfInterest),
                cuts: CreateCuts(spanSegmentsToCut)
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private UInt16 GetCutNodeOfInterestIndex(Guid routeNodeId, RouteNetworkInterest walkOfInterest)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // If node of interest list already contains route node, return that one
            if (_spanEquipment.NodesOfInterestIds.Contains(routeNodeId))
                return (UInt16)Array.IndexOf(_spanEquipment.NodesOfInterestIds, routeNodeId);

            // Find position where to insert new route node interest id
            HashSet<Guid> idsBeforeCut = new HashSet<Guid>();

            foreach (var routeNetworkElement in walkOfInterest.RouteNetworkElementRefs)
            {
                if (routeNetworkElement == routeNodeId)
                    break;

                idsBeforeCut.Add(routeNetworkElement);
            }
       
            for (UInt16 nodeOfInterestIndex = 0; nodeOfInterestIndex <= _spanEquipment.NodesOfInterestIds.Length; nodeOfInterestIndex++)
            {
                Guid nodeOfInterestId = _spanEquipment.NodesOfInterestIds[nodeOfInterestIndex];

                if (!idsBeforeCut.Contains(nodeOfInterestId))
                    return nodeOfInterestIndex;
            }

            throw new ApplicationException($"Error processing cut command. Cannot calculate the node of interest index where to cut. Span equipment: {this.Id} or command has an invalid state. Cut route node id: {routeNodeId} Span equipment walk of interest: {string.Join(",", walkOfInterest.RouteNetworkElementRefs)}");
        }

        private bool IsOuterSpanMissingToBeCut(Guid routeNodeId, HashSet<Guid> spanSegmentsToCut)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // If outer span is already cut at node, no problem
            if (_spanEquipment.SpanStructures[0].SpanSegments.Any(s => 
                _spanEquipment.NodesOfInterestIds[s.FromNodeOfInterestIndex] == routeNodeId ||
                _spanEquipment.NodesOfInterestIds[s.ToNodeOfInterestIndex] == routeNodeId)) 
            {
                return false;
            }

            // If the spanSegmentsToCut include the outer span, no problem
            if (_spanEquipment.SpanStructures[0].SpanSegments.Any(s => spanSegmentsToCut.Contains(s.Id)))
            {
                return false;
            }

            // If we get to here, the outer span is not currently cut and is not being cut as part of the command either
            return true;
        }

        private bool IsAffixedToNodeContainer(Guid routeNodeOrContainerId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.NodeContainerAffixes == null)
                return false;

            foreach (var affix in _spanEquipment.NodeContainerAffixes)
            {
                if (affix.RouteNodeId == routeNodeOrContainerId || affix.NodeContainerId == routeNodeOrContainerId)
                    return true;
            }

            return false;
        }

        private Result IsCutsValid(Guid routeNodeId, HashSet<Guid> spanSegmentsToCut)
        {
            HashSet<Guid> spanSegmentsCutValidatedOk = new HashSet<Guid>();

            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // Check that span segments are not already cut
            foreach (var structure in _spanEquipment.SpanStructures)
            {
                foreach (var segment in  structure.SpanSegments)
                {
                    if (spanSegmentsToCut.Contains(segment.Id))
                    {
                        // Check if span equipment already cut at node
                        if (_spanEquipment.NodesOfInterestIds[segment.FromNodeOfInterestIndex] == routeNodeId || _spanEquipment.NodesOfInterestIds[segment.ToNodeOfInterestIndex] == routeNodeId)
                        {
                            return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                                CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_ALREADY_CUT,
                                $"The span segment with id: {segment.Id} is already cut in route node with id: {routeNodeId}")
                            );
                        }
                        else
                        {
                            spanSegmentsCutValidatedOk.Add(segment.Id);
                        }
                    }
                }
            }


            // Check that we found all span segments
            var notFoundList = new List<Guid>();
            
            foreach (var segmentToCut in spanSegmentsToCut)
            {
                if (!spanSegmentsCutValidatedOk.Contains(segmentToCut))
                    notFoundList.Add(segmentToCut);
            }

            if (notFoundList.Count > 0)
            {
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                                CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND,
                                $"The span segment with ids: {string.Join(",", notFoundList)} was not found in span equipment with id: {this.Id} Notice that you cannot cut span segments belonging to multiple span equipments in the same command!")
                            );
            }

            return Result.Ok();
        }

        private SpanSegmentCutInfo[] CreateCuts(Guid[] spanSegmentsToCut)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            List<SpanSegmentCutInfo> cuts = new List<SpanSegmentCutInfo>();

            foreach (var spanSegmentId in spanSegmentsToCut)
            {
                if (!_spanEquipment.TryGetSpanSegment(spanSegmentId, out SpanSegmentWithIndexInfo spanSegmentWithIndexInfo))
                    throw new ApplicationException("Provided span equipment ids are not valid. The CreateCuts function should not be called before all cuts are proper validated!");

                cuts.Add(
                    new SpanSegmentCutInfo(
                       oldSpanSegmentId: spanSegmentId,
                       newSpanSegmentId1: Guid.NewGuid(),
                       newSpanSegmentId2: Guid.NewGuid()
                       )
                    );
            }

            return cuts.ToArray();
        }

        private void Apply(SpanSegmentsCut @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Undo Cut 

        public Result RevertCut(CommandContext cmdContext, Guid routeNodeId, Guid spanEquipmentId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // If outer span is not cut, do nothing
            if (!_spanEquipment.SpanStructures[0].SpanSegments.Any(s =>
                _spanEquipment.NodesOfInterestIds[s.FromNodeOfInterestIndex] == routeNodeId ||
                _spanEquipment.NodesOfInterestIds[s.ToNodeOfInterestIndex] == routeNodeId))
            {
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.CANNOT_REVERT_SPAN_EQUIPMENT_CUT_DUE_TO_NOT_BEING_CUT, $"Cannot revert cut of span equipment with id: {this.Id} in node: {routeNodeId} because span equipment has not yet been cut in that node."));
            }


            var validUndoCutResult = IsUndoCutValid(routeNodeId);

            if (validUndoCutResult.IsFailed)
                return validUndoCutResult;

            var @event = new SpanEquipmentCutReverted(
              spanEquipmentId: this.Id,
              cutNodeOfInterestId: routeNodeId,
              reverts: CreateCutReverts(routeNodeId)
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private SpanSegmentCutRevertInfo[] CreateCutReverts(Guid routeNodeId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            List<SpanSegmentCutRevertInfo> reverts = new List<SpanSegmentCutRevertInfo>();

            // Check that no span segments cut are connected to other span equipments
            foreach (var structure in _spanEquipment.SpanStructures)
            {
                SpanSegment? previousSegment = null;

                foreach (var segment in structure.SpanSegments)
                {
                    // If span segment is right to a cut
                    if (_spanEquipment.NodesOfInterestIds[segment.FromNodeOfInterestIndex] == routeNodeId)
                    {
                        if (previousSegment == null)
                            throw new ApplicationException($"Invalid segment data in span equipment: {this.Id} structure with id: {structure.Id}");

                        reverts.Add(new SpanSegmentCutRevertInfo(previousSegment.Id, segment.Id, Guid.NewGuid()));
                    }

                    previousSegment = segment;
                }
            }

            return reverts.ToArray();
        }

        private Result IsUndoCutValid(Guid routeNodeId)
        {
            HashSet<Guid> spanSegmentsCutValidatedOk = new HashSet<Guid>();

            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // Check that no span segments cut are connected to other span equipments
            foreach (var structure in _spanEquipment.SpanStructures)
            {
                foreach (var segment in structure.SpanSegments)
                {
                    // If span segment is left to a cut
                    if (_spanEquipment.NodesOfInterestIds[segment.ToNodeOfInterestIndex] == routeNodeId)
                    {
                        // If connected
                        if (segment.ToTerminalId != Guid.Empty)
                        {
                            // If not connected to a segment in same structure then we cannot revert
                            if (!structure.SpanSegments.Any(s => s.FromTerminalId == segment.ToTerminalId))
                            {
                                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.CANNOT_REVERT_SPAN_EQUIPMENT_CUT_DUE_TO_CONNECTED_SEGMENT, $"Cannot revert cut of span equipment with id: {this.Id} in node: {routeNodeId} because span segment with id: {segment.Id} is connected in way that cannot be reverted."));
                            }
                        }
                    }

                    // If span segment is left to a cut
                    if (_spanEquipment.NodesOfInterestIds[segment.FromNodeOfInterestIndex] == routeNodeId)
                    {
                        // If connected
                        if (segment.FromTerminalId != Guid.Empty)
                        {
                            // If not connected to a segment in same structure then we cannot revert
                            if (!structure.SpanSegments.Any(s => s.ToTerminalId == segment.FromTerminalId))
                            {
                                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.CANNOT_REVERT_SPAN_EQUIPMENT_CUT_DUE_TO_CONNECTED_SEGMENT, $"Cannot revert cut of span equipment with id: {this.Id} in node: {routeNodeId} because span segment with id: {segment.Id} is connected in way that cannot be reverted."));
                            }
                        }
                    }
                }
            }

            return Result.Ok();
        }

        private void Apply(SpanEquipmentCutReverted @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Connect Span Segments To Simple Terminals
        public Result ConnectConduitSpanSegmentsToSimpleTerminals(CommandContext cmdContext, SpanEquipmentSpecification specification, Guid routeNodeId, SpanSegmentToSimpleTerminalConnectInfo[] connects)
        {
            if (routeNodeId == Guid.Empty)
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_ROUTE_NODE_ID_CANNOT_BE_EMPTY, "Route node id cannot be empty."));

            if (specification.IsCable)
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_KIND_MUST_BE_NON_CABLE, "Can only call this function on non-cable span equipments"));

            // Chat that span equipment is affixed to container at node where the connects should be created
            if (!IsAffixedToNodeContainer(routeNodeId))
            {
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                   ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_EQUIPMENT_NOT_AFFIXED_TO_NODE_CONTAINER,
                   $"Connecting span segments is only allowed if the span equipment: {this.Id} is affixed to a node container in route node: {routeNodeId}")
               );
            }

            // Check that connects are valid
            var validConnectsResult = IsConnectsValid(routeNodeId, connects);

            if (validConnectsResult.IsFailed)
                return validConnectsResult;

            var @event = new SpanSegmentsConnectedToSimpleTerminals(
                spanEquipmentId: this.Id,
                connects: connects
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private Result IsConnectsValid(Guid routeNodeId, SpanSegmentToSimpleTerminalConnectInfo[] connects)
        {
            HashSet<Guid> spanSegmentsCutValidatedOk = new HashSet<Guid>();

            Dictionary<Guid, SpanSegmentToSimpleTerminalConnectInfo> connectsBySegmentId = connects.ToDictionary(c => c.SegmentId);

            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // Check that the same span segment is not connected multiple times
            HashSet<Guid> uniqueSpanSegmentIds = new();

            foreach (var connect in connects)
            {
                if (uniqueSpanSegmentIds.Contains(connect.SegmentId))
                {
                    return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                               ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_CONNECTED_MORE_THAN_ONCE,
                               $"The segment with id: {connect.SegmentId} is connected several times. Must be unique.")
                           );
                }
                else
                {
                    uniqueSpanSegmentIds.Add(connect.SegmentId);
                }
            }


            // Check that alle span segments connected are ending in the route node
            foreach (var structure in _spanEquipment.SpanStructures)
            {
                foreach (var segment in structure.SpanSegments)
                {
                    if (connectsBySegmentId.TryGetValue(segment.Id, out var spanSegmentToSimpleTerminalConnectInfo))
                    {
                        // Check if span segment is connected to route node where to cut
                        if (_spanEquipment.NodesOfInterestIds[segment.FromNodeOfInterestIndex] == routeNodeId)
                        {
                            spanSegmentToSimpleTerminalConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromTerminalToSpanSegment;
                        }
                        else if (_spanEquipment.NodesOfInterestIds[segment.ToNodeOfInterestIndex] == routeNodeId)
                        {
                            spanSegmentToSimpleTerminalConnectInfo.ConnectionDirection = SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal;
                        }
                        else 
                        { 
                            return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                                ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_END_NOT_FOUND,
                                $"No ends of the span segment with id: {segment.Id} can be found in route node with id: {routeNodeId}")
                            );
                        }

                        // Check if already connected
                        if (spanSegmentToSimpleTerminalConnectInfo.ConnectionDirection == SpanSegmentToTerminalConnectionDirection.FromSpanSegmentToTerminal && segment.ToTerminalId != Guid.Empty)
                        {
                            return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                                   ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_ALREADY_CONNECTED,
                                   $"Span segment with id: {segment.Id} already connected to a terminal with id: {segment.ToTerminalId}")
                               );
                        }

                        if (spanSegmentToSimpleTerminalConnectInfo.ConnectionDirection == SpanSegmentToTerminalConnectionDirection.FromTerminalToSpanSegment && segment.FromTerminalId != Guid.Empty)
                        {
                            return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(
                                   ConnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_ALREADY_CONNECTED,
                                   $"Span segment with id: {segment.Id} already connected from a terminal with id: {segment.FromTerminalId}")
                               );
                        }

                        spanSegmentsCutValidatedOk.Add(segment.Id);
                    }
                }
            }


            // Check that we found all span segments
            var notFoundList = new List<Guid>();

            foreach (var segmentToConnect in connectsBySegmentId.Keys)
            {
                if (!spanSegmentsCutValidatedOk.Contains(segmentToConnect))
                    notFoundList.Add(segmentToConnect);
            }

            if (notFoundList.Count > 0)
            {
                return Result.Fail(new CutSpanSegmentsAtRouteNodeError(
                                CutSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND,
                                $"The span segment with ids: {string.Join(",", notFoundList)} was not found in span equipment with id: {this.Id} Notice that you cannot connect span segments belonging to multiple span equipments to terminal in the same command!")
                            );
            }

            return Result.Ok();
        }

        private void Apply(SpanSegmentsConnectedToSimpleTerminals @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }
        #endregion

        #region Connect Cable Span Segments With Terminals
        public Result ConnectCableSpanSegmentsWithTerminals(CommandContext cmdContext, SpanEquipmentSpecification specification, Guid routeNodeId, SpanSegmentToSimpleTerminalConnectInfo[] connects)
        {
            if (routeNodeId == Guid.Empty)
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_ROUTE_NODE_ID_CANNOT_BE_EMPTY, "Route node id cannot be empty."));

            if (!specification.IsCable)
                return Result.Fail(new ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes.INVALID_SPAN_SEGMENT_KIND_MUST_BE_CABLE, "Can only call this function on cable span equipments"));


            // Check that connects are valid
            var validConnectsResult = IsConnectsValid(routeNodeId, connects);

            if (validConnectsResult.IsFailed)
                return validConnectsResult;

            var @event = new SpanSegmentsConnectedToSimpleTerminals(
                spanEquipmentId: this.Id,
                connects: connects
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }


        #endregion

        #region Disconnect Span Segment From Terminal
        public Result DisconnectSegmentFromTerminal(CommandContext cmdContext, Guid spanSegmentId, Guid terminalId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (!_spanEquipment.TryGetSpanSegment(spanSegmentId, out var spanSegmentWithIndexInfo))
            {
                return Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment with id: {spanSegmentId} in span equipment with id: {this.Id}"));
            }

            if (!IsConnected(spanSegmentWithIndexInfo.SpanSegment, terminalId))
            {
                return Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_CONNECTED_TO_TERMINAL, $"Span segment with id: {spanSegmentId} not connected to termian: {terminalId} in span equipment with id: {this.Id}"));
            }

            var @event = new SpanSegmentDisconnectedFromTerminal(
               spanEquipmentId: this.Id,
               spanSegmentId: spanSegmentId,
               terminalId: terminalId
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanSegmentDisconnectedFromTerminal @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        private bool IsConnected(SpanSegment spanSegment, Guid terminalId)
        {
            if (spanSegment.FromTerminalId == terminalId || spanSegment.ToTerminalId == terminalId)
                return true;
            else
                return false;
        }

        #endregion

        #region Disconnect Segments From Terminals
        public Result DisconnectSegmentsFromTerminals(CommandContext cmdContext, DisconnectSpanSegmentFromTerminalOperation[] disconnects)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // Disconnect records to be include in event
            List<SpanSegmentToTerminalDisconnectInfo> disconnectInfos = new();

            // Check that each span segment specified exists and is connected to terminal id specified
            foreach (var disconnect in disconnects)
            {
                if (_spanEquipment.TryGetSpanSegment(disconnect.SpanSegmentId, out var spanSegmentWithIndexInfo))
                {
                    var spanSegment = spanSegmentWithIndexInfo.SpanSegment;

                    if (spanSegment.FromTerminalId != disconnect.TerminalId && spanSegment.ToTerminalId != disconnect.TerminalId)
                    {
                        return Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_CONNECTED_TO_TERMINAL, $"The span segment with id: {disconnect.SpanSegmentId} in span equipment with id: {this.Id} is not connected to any terminal with id: {disconnect.TerminalId}"));
                    }
                }
                else
                {
                    return Result.Fail(new DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes.SPAN_SEGMENT_NOT_FOUND, $"Cannot find any span segment with id: {disconnect.SpanSegmentId} in span equipment with id: {this.Id}"));
                }

                disconnectInfos.Add(new SpanSegmentToTerminalDisconnectInfo(disconnect.SpanSegmentId, disconnect.TerminalId));
            }

            var @event = new SpanSegmentsDisconnectedFromTerminals(
               spanEquipmentId: this.Id,
               disconnects: disconnectInfos.ToArray()
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();

        }

        private void Apply(SpanSegmentsDisconnectedFromTerminals @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Add Additional Structures
        public Result AddAdditionalStructures(CommandContext cmdContext, SpanEquipmentSpecification specification, LookupCollection<SpanStructureSpecification> structureSpecifications, Guid[] structureSpecificationIdsToAdd)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (structureSpecifications.Count == 0)
            {
                return Result.Fail(new PlaceAdditionalStructuresInSpanEquipmentError(
                   PlaceAdditionalStructuresInSpanEquipmentErrorCodes.INVALID_STRUCTURE_SPECIFICAION_ID_LIST_CANNOT_BE_EMPTY,
                   $"Must provide at least 1 structure specification id")
               );
            }

            // Check that all specifications are there
            foreach (var structureSpecificationId in structureSpecificationIdsToAdd)
            {
                if (!structureSpecifications.ContainsKey(structureSpecificationId))
                {
                    return Result.Fail(new PlaceAdditionalStructuresInSpanEquipmentError(
                        PlaceAdditionalStructuresInSpanEquipmentErrorCodes.INVALID_STRUCTURE_SPECIFICATION_ID_NOT_FOUND,
                        $"Cannot find structure specification with id: {structureSpecificationId}")
                    );
                }
            }

            if (specification.IsFixed)
            {
                return Result.Fail(new PlaceAdditionalStructuresInSpanEquipmentError(
                    PlaceAdditionalStructuresInSpanEquipmentErrorCodes.CANNOT_ADD_SPAN_STRUCTURES_TO_FIXED_SPAN_EQUIPMENT,
                    $"Cannot add span structures to a span equipment with fixed structure - i.e. to a span equipment with pre-installed inner conduits")
                );
            }

            List<SpanStructure> spanStructuresToInclude = new List<SpanStructure>();

            var innerStructureStartPosition = 1;

            if (_spanEquipment.SpanStructures.Any(s => s.Level == 2))
                innerStructureStartPosition = _spanEquipment.SpanStructures.Where(s => s.Level == 2).Max(s => s.Position) + 1;
                       
            // Add structures to level 2
            foreach (var structureSpecificationId in structureSpecificationIdsToAdd)
            {
                spanStructuresToInclude.Add(
                    new SpanStructure(
                        id: Guid.NewGuid(),
                        specificationId: structureSpecificationId,
                        level: 2,
                        parentPosition: 1,
                        position: (ushort)innerStructureStartPosition,
                        spanSegments: new SpanSegment[] { new SpanSegment(Guid.NewGuid(), 0, (ushort)(_spanEquipment.NodesOfInterestIds.Length - 1)) }
                    )
                );

                innerStructureStartPosition++;
            }

            var @event = new AdditionalStructuresAddedToSpanEquipment(
               spanEquipmentId: this.Id,
               spanStructuresToAdd: spanStructuresToInclude.ToArray()
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(AdditionalStructuresAddedToSpanEquipment @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Remove Span Structure

        public Result RemoveSpanStructure(CommandContext cmdContext, SpanEquipmentSpecification specification, ushort structureIndex)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (specification.IsFixed)
            {
                return Result.Fail(new RemoveSpanStructureFromSpanEquipmentError(
                    RemoveSpanStructureFromSpanEquipmentErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURES_FROM_FIXED_SPAN_EQUIPMENT,
                    $"Cannot remove span structures from a span equipment with fixed structure - i.e. from a span equipment with pre-installed inner conduits")
                ); 
            }

            if (structureIndex == 0)
                throw new ApplicationException("You can't use the RemoveSpanStructure method to remove the outer span structure. In such case you need to remove the entire span equipment.");

            if (structureIndex >= _spanEquipment.SpanStructures.Length || structureIndex < 0)
                throw new ApplicationException("Structure index out of bounds.");

            if (IsAnySpanSegmentsInStructureConnected(structureIndex))
            {
                return Result.Fail(new RemoveSpanStructureFromSpanEquipmentError(
                    RemoveSpanStructureFromSpanEquipmentErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURE_WITH_CONNECTED_SEGMENTS_FROM_SPAN_EQUIPMENT,
                    $"Cannot remove span structures from a span equipment if some of its segments are connected")
                );
            }


            var spanStructureToRemove = _spanEquipment.SpanStructures[structureIndex];

            var @event = new SpanStructureRemoved(
               spanEquipmentId: this.Id,
               spanStructureId: spanStructureToRemove.Id
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private bool IsAnySpanSegmentsInStructureConnected(ushort structureIndex)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            var spanStructure = _spanEquipment.SpanStructures[structureIndex];

            foreach (var spanSegment in spanStructure.SpanSegments)
            {
                if (spanSegment.FromTerminalId != Guid.Empty || spanSegment.ToTerminalId != Guid.Empty)
                    return true;
            }

            return false;
        }

        private void Apply(SpanStructureRemoved @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Remove
        public Result Remove(CommandContext cmdContext)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (IsAnySpanSegmentsConnected())
            {
                return Result.Fail(new RemoveSpanStructureFromSpanEquipmentError(
                    RemoveSpanStructureFromSpanEquipmentErrorCodes.CANNOT_REMOVE_SPAN_EQUIPMENT_WITH_CONNECTED_SEGMENTS,
                    $"Cannot remove a span equipment if some of its segments are connected")
                );
            }

            var @event = new SpanEquipmentRemoved(
               spanEquipmentId: this.Id
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanEquipmentRemoved @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");
        }

        private bool IsAnySpanSegmentsConnected()
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            foreach (var spanStructure in _spanEquipment.SpanStructures)
            {
                foreach (var spanSegment in spanStructure.SpanSegments)
                {
                    if (spanSegment.FromTerminalId != Guid.Empty || spanSegment.ToTerminalId != Guid.Empty)
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Move
        public Result MoveAsPartOfAffix(CommandContext cmdContext, ValidatedRouteNetworkWalk newWalk, ValidatedRouteNetworkWalk existingWalk)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // If both ends are moved, there cannot be any cuts/breakouts.
            if (newWalk.FromNodeId != existingWalk.FromNodeId && newWalk.ToNodeId != existingWalk.ToNodeId && _spanEquipment.NodesOfInterestIds.Length > 2)
            {
                return Result.Fail(new MoveSpanEquipmentError(
                   MoveSpanEquipmentErrorCodes.CANNOT_MOVE_BOTH_ENDS_AT_THE_SAME_TIME_IF_SPAN_SEGMENT_HAS_CUTS,
                   $"Cannot move both ends of the walk at the same time, when the span equipment has cuts/breakouts. This because we then have no idea if the walk has been reversed, which might lead to inconsistency in the segment connectivity direction inside the span equipment. The user has to move one end at the time.")
               );
            }

            // If from end is moved
            if (newWalk.FromNodeId != existingWalk.FromNodeId)
            {
                // There cannot be any connection in the node moved away from
                if (IsAnySpanSegmentsConnectedInNode(existingWalk.FromNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE,
                       $"Cannot move from end from: {existingWalk.FromNodeId} to: {newWalk.FromNodeId} because segments exists that has connections to other equipment in that node.")
                   );
                }

                // There cannot be any cuts in the node moved to
                if (IntermediateCutNodeIds.Contains(newWalk.FromNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT,
                       $"Cannot move from end from: {existingWalk.FromNodeId} to: {newWalk.FromNodeId} because segments exists that are cut in that node.")
                   );
                }
            }

            // If to end is moved
            if (newWalk.ToNodeId != existingWalk.ToNodeId)
            {
                // There cannot be any connection in the node moved away from
                if (IsAnySpanSegmentsConnectedInNode(existingWalk.ToNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE,
                       $"Cannot move to end from: {existingWalk.ToNodeId} to: {newWalk.ToNodeId} because segments exists that has connections to other equipment in that node.")
                   );
                }

                // There cannot be any cuts in the node moved to
                if (IntermediateCutNodeIds.Contains(newWalk.ToNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT,
                       $"Cannot move to end from: {existingWalk.ToNodeId} to: {newWalk.ToNodeId} because segments exists that are cut in that node.")
                   );
                }
            }

            // Check that span equipment is not moved away from nodes where it is cut
            foreach (var nodeOfInterestId in IntermediateCutNodeIds)
            {
                if (!newWalk.RouteNetworkElementRefs.Contains(nodeOfInterestId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                        MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SEGMENTS_ARE_CUT_THERE,
                        $"Cannot move span equipment away from node: {nodeOfInterestId} because segments are cut in this node.")
                    );
                }
            }

            // Check that span equipment is not moved away from nodes where it is affixed to a container
            if (_spanEquipment.NodeContainerAffixes != null)
            {
                foreach (var nodeContainerAffix in _spanEquipment.NodeContainerAffixes)
                {
                    if (!newWalk.RouteNetworkElementRefs.Contains(nodeContainerAffix.RouteNodeId))
                    {
                        return Result.Fail(new MoveSpanEquipmentError(
                            MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SPAN_EQUIPMENT_IS_AFFIXED_TO_CONTAINER,
                            $"Cannot move span equipment away from node: {nodeContainerAffix.RouteNodeId} because span equipment is affixed to a container in this node.")
                        );
                    }
                }
            }

            var @event = new SpanEquipmentMoved(
              spanEquipmentId: this.Id,
              nodesOfInterestIds: CreateNewNodesOfInterestIdList(newWalk)
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();

        }

        public Result Move(CommandContext cmdContext, ValidatedRouteNetworkWalk newWalk, ValidatedRouteNetworkWalk existingWalk)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            // If both ends are moved, there cannot be any cuts/breakouts.
            if (newWalk.FromNodeId != existingWalk.FromNodeId && newWalk.ToNodeId != existingWalk.ToNodeId && _spanEquipment.NodesOfInterestIds.Length > 2)
            {
                return Result.Fail(new MoveSpanEquipmentError(
                   MoveSpanEquipmentErrorCodes.CANNOT_MOVE_BOTH_ENDS_AT_THE_SAME_TIME_IF_SPAN_SEGMENT_HAS_CUTS,
                   $"Cannot move both ends of the walk at the same time, when the span equipment has cuts/breakouts. This because we then have no idea if the walk has been reversed, which might lead to inconsistency in the segment connectivity direction inside the span equipment. The user has to move one end at the time.")
               );
            }

            // If from end is moved
            if (newWalk.FromNodeId != existingWalk.FromNodeId)
            {
                // There cannot be any connection in the node moved away from
                if (IsAnySpanSegmentsConnectedInNode(existingWalk.FromNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE,
                       $"Cannot move from end from: {existingWalk.FromNodeId} to: {newWalk.FromNodeId} because segments exists that has connections to other equipment in that node.")
                   );
                }

                // There cannot be any cuts in the node moved to
                if (IntermediateCutNodeIds.Contains(newWalk.FromNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT,
                       $"Cannot move from end from: {existingWalk.FromNodeId} to: {newWalk.FromNodeId} because segments exists that are cut in that node.")
                   );
                }
            }

            // If to end is moved
            if (newWalk.ToNodeId != existingWalk.ToNodeId)
            {
                // There cannot be any connection in the node moved away from
                if (IsAnySpanSegmentsConnectedInNode(existingWalk.ToNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE,
                       $"Cannot move to end from: {existingWalk.ToNodeId} to: {newWalk.ToNodeId} because segments exists that has connections to other equipment in that node.")
                   );
                }

                // There cannot be any cuts in the node moved to
                if (IntermediateCutNodeIds.Contains(newWalk.ToNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT,
                       $"Cannot move to end from: {existingWalk.ToNodeId} to: {newWalk.ToNodeId} because segments exists that are cut in that node.")
                   );
                }
            }

            // Check that span equipment is not moved away from nodes where it is cut
            foreach (var nodeOfInterestId in IntermediateCutNodeIds)
            {
                if (!newWalk.RouteNetworkElementRefs.Contains(nodeOfInterestId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                        MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SEGMENTS_ARE_CUT_THERE,
                        $"Cannot move span equipment away from node: {nodeOfInterestId} because segments are cut in this node.")
                    );
                }
            }

            // Check that span equipment is not moved away from nodes where it is affixed to a container
            if (_spanEquipment.NodeContainerAffixes != null)
            {
                foreach (var nodeContainerAffix in _spanEquipment.NodeContainerAffixes)
                {
                    if (!newWalk.RouteNetworkElementRefs.Contains(nodeContainerAffix.RouteNodeId))
                    {
                        return Result.Fail(new MoveSpanEquipmentError(
                            MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SPAN_EQUIPMENT_IS_AFFIXED_TO_CONTAINER,
                            $"Cannot move span equipment away from node: {nodeContainerAffix.RouteNodeId} because span equipment is affixed to a container in this node.")
                        );
                    }
                }
            }
                        
            // Check that span equipment is not moved where affixed to parents conduits
            if (IsAnyParentSubwalksMoved(existingWalk, newWalk))
            {
                return Result.Fail(new MoveSpanEquipmentError(
                   MoveSpanEquipmentErrorCodes.CANNOT_MOVE_SEGMENTS_AFFIXED_TO_PARENTS,
                   $"Cannot move span equipment with id: {this.Id}. Sub walks affixed to parent span equipments are moved which is not a legal operation.")
               );
            }
            
            var @event = new SpanEquipmentMoved(
              spanEquipmentId: this.Id,
              nodesOfInterestIds: CreateNewNodesOfInterestIdList(newWalk)
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();

        }

        public Result<ValidatedRouteNetworkWalk> MoveWithParent(CommandContext cmdContext, ValidatedRouteNetworkWalk existingChildWalk, SpanEquipment parentMoved, ValidatedRouteNetworkWalk newParentWalk, ValidatedRouteNetworkWalk previousParentWalk, UtilityNetworkHopQueryHelper hopQueryHelper)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            var newWalkResult = CalculateNewWalkFollowingNewParentWalk(existingChildWalk, parentMoved, newParentWalk, previousParentWalk, hopQueryHelper);

            if (newWalkResult.IsFailed)
                return Result.Fail(newWalkResult.Errors.First());

            var newWalk = newWalkResult.Value.Item1;

            // If both ends are moved, there cannot be any cuts/breakouts.
            if (newWalk.FromNodeId != existingChildWalk.FromNodeId && newWalk.ToNodeId != existingChildWalk.ToNodeId && _spanEquipment.NodesOfInterestIds.Length > 2)
            {
                return Result.Fail(new MoveSpanEquipmentError(
                   MoveSpanEquipmentErrorCodes.CANNOT_MOVE_BOTH_ENDS_AT_THE_SAME_TIME_IF_SPAN_SEGMENT_HAS_CUTS,
                   $"Cannot move both ends of the walk at the same time, when the span equipment has cuts/breakouts. This because we then have no idea if the walk has been reversed, which might lead to inconsistency in the segment connectivity direction inside the span equipment. The user has to move one end at the time.")
               );
            }

            bool endsMoved = false;

            // If from end is moved
            if (newWalk.FromNodeId != existingChildWalk.FromNodeId)
            {
                endsMoved = true;

                // There cannot be any connection in the node moved away from
                if (IsAnySpanSegmentsConnectedInNode(existingChildWalk.FromNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE,
                       $"Cannot move from end from: {existingChildWalk.FromNodeId} to: {newWalk.FromNodeId} because segments exists that has connections to other equipment in that node.")
                   );
                }

                // There cannot be any cuts in the node moved to
                if (IntermediateCutNodeIds.Contains(newWalk.FromNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_FROM_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT,
                       $"Cannot move from end from: {existingChildWalk.FromNodeId} to: {newWalk.FromNodeId} because segments exists that are cut in that node.")
                   );
                }
            }

            // If to end is moved
            if (newWalk.ToNodeId != existingChildWalk.ToNodeId)
            {
                endsMoved = true;

                // There cannot be any connection in the node moved away from
                if (IsAnySpanSegmentsConnectedInNode(existingChildWalk.ToNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_BECAUSE_SEGMENTS_ARE_CONNECTED_THERE,
                       $"Cannot move to end from: {existingChildWalk.ToNodeId} to: {newWalk.ToNodeId} because segments exists that has connections to other equipment in that node.")
                   );
                }

                // There cannot be any cuts in the node moved to
                if (IntermediateCutNodeIds.Contains(newWalk.ToNodeId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                       MoveSpanEquipmentErrorCodes.CANNOT_MOVE_TO_END_TO_NODE_WHERE_SEGMENTS_ARE_CUT,
                       $"Cannot move to end from: {existingChildWalk.ToNodeId} to: {newWalk.ToNodeId} because segments exists that are cut in that node.")
                   );
                }
            }

            // Check that span equipment is not moved away from nodes where it is cut
            foreach (var nodeOfInterestId in IntermediateCutNodeIds)
            {
                if (!newWalk.RouteNetworkElementRefs.Contains(nodeOfInterestId))
                {
                    return Result.Fail(new MoveSpanEquipmentError(
                        MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SEGMENTS_ARE_CUT_THERE,
                        $"Cannot move span equipment away from node: {nodeOfInterestId} because segments are cut in this node.")
                    );
                }
            }

            // Check that span equipment is not moved away from nodes where it is affixed to a container
            if (_spanEquipment.NodeContainerAffixes != null)
            {
                foreach (var nodeContainerAffix in _spanEquipment.NodeContainerAffixes)
                {
                    if (!newWalk.RouteNetworkElementRefs.Contains(nodeContainerAffix.RouteNodeId))
                    {
                        return Result.Fail(new MoveSpanEquipmentError(
                            MoveSpanEquipmentErrorCodes.CANNOT_MOVE_NODE_BECAUSE_SPAN_EQUIPMENT_IS_AFFIXED_TO_CONTAINER,
                            $"Cannot move span equipment away from node: {nodeContainerAffix.RouteNodeId} because span equipment is affixed to a container in this node.")
                        );
                    }
                }
            }

            if (endsMoved)
            {

                var @event = new SpanEquipmentMoved(
                  spanEquipmentId: this.Id,
                  nodesOfInterestIds: CreateNewNodesOfInterestIdList(newWalk)
                )
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                };

                RaiseEvent(@event);
            }

            // Update utility network hops if ends has omoved
            if (newWalkResult.Value.Item2.Count > 0)
            {
                var hopsToReplace = newWalkResult.Value.Item2;
                List<UtilityNetworkHop> newUtilityNetworkHopList = new();

                for (int i = 0; i < _spanEquipment.UtilityNetworkHops.Length; i++)
                {
                    if (hopsToReplace.ContainsKey(i))
                        newUtilityNetworkHopList.Add(hopsToReplace[i]);
                    else
                        newUtilityNetworkHopList.Add(_spanEquipment.UtilityNetworkHops[i]);
                }


                RaiseEvent(
                    new SpanEquipmentAffixedToParent(this.Id, newUtilityNetworkHopList.ToArray())
                    {
                        CorrelationId = cmdContext.CorrelationId,
                        IncitingCmdId = cmdContext.CmdId,
                        UserName = cmdContext.UserContext?.UserName,
                        WorkTaskId = cmdContext.UserContext?.WorkTaskId
                    }
                );


            }


            return Result.Ok(newWalk);

        }

        private Result<(ValidatedRouteNetworkWalk, Dictionary<int, UtilityNetworkHop>)> CalculateNewWalkFollowingNewParentWalk(ValidatedRouteNetworkWalk existingChildWalkOfInterest, SpanEquipment parentMoved, ValidatedRouteNetworkWalk newParentWalk, ValidatedRouteNetworkWalk previousParentWalk, UtilityNetworkHopQueryHelper hopQueryHelper)
        {
            HashSet<Guid> walkIds = newParentWalk.NodeIds.ToHashSet<Guid>();

            var impactedChildHops = FindUtilityNetworkHopSegmentsReferencingSpanEquipment(parentMoved);

            var childNetworkHops = FindRouteNetworkHops(existingChildWalkOfInterest);

            // Check if child (this span segment) has to be moved at all
            bool allImpactedChildSegmentsIsCoveredByNewParentWalk = true;

            foreach (var childNetworkHop in childNetworkHops.Where(n => n.IsUtilityHop))
            {
                if (impactedChildHops.ContainsKey(childNetworkHop.UtilityNetworkHop))
                {
                    var subWalks = hopQueryHelper.GetSubwalks(childNetworkHop.UtilityNetworkHop);

                    foreach (var subWalk in subWalks)
                    {
                        foreach (var routeSegmentId in subWalk.Walk.SegmentIds)
                        {
                            if (!newParentWalk.SegmentIds.Contains(routeSegmentId))
                                allImpactedChildSegmentsIsCoveredByNewParentWalk = false;
                        }
                    }
                }
            }

            bool childEndHasBeenDragged = false;

            // If parent from node has changed, and child has start or end in same node, then the child has to be "dragged"
            if (newParentWalk.FromNodeId != previousParentWalk.FromNodeId && (existingChildWalkOfInterest.FromNodeId == previousParentWalk.FromNodeId || existingChildWalkOfInterest.ToNodeId == previousParentWalk.FromNodeId))
            {
                childEndHasBeenDragged = true;
            }

            // If parent to node has changed, and child has start or end in same node, then the child has to be "dragged"
            if (newParentWalk.ToNodeId != previousParentWalk.ToNodeId && (existingChildWalkOfInterest.FromNodeId == previousParentWalk.ToNodeId || existingChildWalkOfInterest.ToNodeId == previousParentWalk.ToNodeId))
            {
                childEndHasBeenDragged = true;
            }

            // If child has not been dragged and impacted section is within new parent walk, then child walk is the same
            if (!childEndHasBeenDragged && allImpactedChildSegmentsIsCoveredByNewParentWalk)
            {
                return Result.Ok((existingChildWalkOfInterest, new Dictionary<int, UtilityNetworkHop>() { } ));
            }


            // Calculate new walk for the child (this segment)
            Dictionary<int, UtilityNetworkHop> utilityNetworkHopsToUpdateByIndex = new(); 

            foreach (var networkHop in childNetworkHops.Where(n => n.IsUtilityHop))
            {
                if (impactedChildHops.ContainsKey(networkHop.UtilityNetworkHop))
                {
                    var childSpanSegmentsImpacted = impactedChildHops[networkHop.UtilityNetworkHop];

                    var childSubWalks = hopQueryHelper.GetSubwalks(networkHop.UtilityNetworkHop);

                    List<Guid> newNetworkHopWalkIds = new();

                    bool moved = false;
                    bool firstSubWalk = true;
                    bool lastSubWalk = false;

                    foreach (var childSubWalk in childSubWalks)
                    {
                        if (childSubWalk == childSubWalks.Last())
                            lastSubWalk = true; 

                        if (childSpanSegmentsImpacted.Contains(childSubWalk.ParentSegementId))
                        {
                            // Walk start and end is not changed
                            if (childSubWalk.Walk.FromNodeId == newParentWalk.FromNodeId && childSubWalk.Walk.ToNodeId == newParentWalk.ToNodeId)
                            {
                                moved = true;
                                newNetworkHopWalkIds.AddRange(firstSubWalk ? newParentWalk.RouteNetworkElementRefs : GetElementsExceptFirst(newParentWalk.RouteNetworkElementRefs));
                            }
                            // Walk start and end is not changed (reversed)
                            else if (childSubWalk.Walk.ToNodeId == newParentWalk.FromNodeId && childSubWalk.Walk.FromNodeId == newParentWalk.ToNodeId)
                            {
                                moved = true;
                                newNetworkHopWalkIds.AddRange(firstSubWalk ? GetReversedIds(newParentWalk.RouteNetworkElementRefs) : GetElementsExceptFirst(GetReversedIds(newParentWalk.RouteNetworkElementRefs)));
                            }
                            // Subwalk is first and new walk is extended/shrinked 
                            else if (firstSubWalk && networkHop.IsFirst && childSubWalk.Walk.ToNodeId == newParentWalk.ToNodeId)
                            {
                                moved = true;
                                newNetworkHopWalkIds.AddRange(newParentWalk.RouteNetworkElementRefs);
                                utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(newParentWalk.FromNodeId, networkHop.Walk.ToNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                            }
                            // Subwalk is first and new parent is exteded right (reversed)
                            else if (firstSubWalk && networkHop.IsFirst && childSubWalk.Walk.ToNodeId == newParentWalk.FromNodeId)
                            {
                                moved = true;
                                newNetworkHopWalkIds.AddRange(GetReversedIds(newParentWalk.RouteNetworkElementRefs));
                                utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(newParentWalk.ToNodeId, networkHop.Walk.ToNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                            }
                            // Subwalk is last and new parent is exteded right (reversed)
                            else if (lastSubWalk && networkHop.IsLast && childSubWalk.Walk.FromNodeId == newParentWalk.FromNodeId)
                            {
                                moved = true;
                                newNetworkHopWalkIds.AddRange(firstSubWalk ? newParentWalk.RouteNetworkElementRefs : GetElementsExceptFirst(newParentWalk.RouteNetworkElementRefs));
                                utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(networkHop.Walk.FromNodeId, newParentWalk.ToNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                            }
                            // Subwalk is last and new parent is exteded right (reversed)
                            else if (lastSubWalk && networkHop.IsLast && childSubWalk.Walk.FromNodeId == newParentWalk.ToNodeId)
                            {
                                moved = true;
                                newNetworkHopWalkIds.AddRange(firstSubWalk ? GetReversedIds(newParentWalk.RouteNetworkElementRefs) : GetElementsExceptFirst(GetReversedIds(newParentWalk.RouteNetworkElementRefs)));
                                utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(networkHop.Walk.FromNodeId, newParentWalk.FromNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                            }

                        }
                        else
                        {
                            newNetworkHopWalkIds.AddRange(firstSubWalk ? childSubWalk.Walk.RouteNetworkElementRefs : GetElementsExceptFirst(childSubWalk.Walk.RouteNetworkElementRefs));
                        }

                        firstSubWalk = false;
                    }


                    

                    /*
             
                    // Check if walk start and end is not changed
                    if (networkHop.Walk.FromNodeId == newParentWalk.FromNodeId && networkHop.Walk.ToNodeId == newParentWalk.ToNodeId)
                    {
                        moved = true;
                        newNetworkHopWalkIds.AddRange(newParentWalk.RouteNetworkElementRefs);
                    }
                    // Check if walk start and end is not changed
                    else if (networkHop.Walk.ToNodeId == newParentWalk.FromNodeId && networkHop.Walk.FromNodeId == newParentWalk.ToNodeId)
                    {
                        moved = true;
                        newNetworkHopWalkIds.AddRange(GetReversedIds(newParentWalk.RouteNetworkElementRefs));
                    }
                    // Hop is last and new parent is exteded right
                    else if (networkHop.IsLast && networkHop.Walk.FromNodeId == newParentWalk.FromNodeId)
                    {
                        moved = true;
                        newNetworkHopWalkIds.AddRange(newParentWalk.RouteNetworkElementRefs);
                        utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(newParentWalk.FromNodeId, newParentWalk.ToNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                    }
                    // Hop is last and new parent is exteded right
                    else if (networkHop.IsLast && networkHop.Walk.FromNodeId == newParentWalk.ToNodeId)
                    {
                        moved = true;
                        newNetworkHopWalkIds.AddRange(GetReversedIds(newParentWalk.RouteNetworkElementRefs));
                        utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(newParentWalk.ToNodeId, newParentWalk.FromNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                    }
                    // Hop is first and new parent is exteded right
                    else if (networkHop.IsFirst && networkHop.Walk.ToNodeId == newParentWalk.ToNodeId)
                    {
                        moved = true;
                        newNetworkHopWalkIds.AddRange(newParentWalk.RouteNetworkElementRefs);
                        utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(newParentWalk.FromNodeId, newParentWalk.ToNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                    }
                    // Hop is first and new parent is exteded right
                    else if (networkHop.IsFirst && networkHop.Walk.ToNodeId == newParentWalk.FromNodeId)
                    {
                        moved = true;
                        newNetworkHopWalkIds.AddRange(GetReversedIds(newParentWalk.RouteNetworkElementRefs));
                        utilityNetworkHopsToUpdateByIndex.Add(Array.IndexOf(_spanEquipment.UtilityNetworkHops, networkHop.UtilityNetworkHop), new UtilityNetworkHop(newParentWalk.ToNodeId, newParentWalk.FromNodeId, networkHop.UtilityNetworkHop.ParentAffixes));
                    }
                    */

                    if (!moved)
                        return Result.Fail(new MoveSpanEquipmentError(MoveSpanEquipmentErrorCodes.ERROR_MOVING_CHILD_SPAN_EQUIPMENT, $"Error moving child span equipment with id: {this.Id} to walk of interest of new parent span equipment with id: {parentMoved.Id}"));

                    var idList = new RouteNetworkElementIdList();
                    idList.AddRange(newNetworkHopWalkIds);

                    networkHop.Walk = new ValidatedRouteNetworkWalk(idList);
                }
            }

            var newWalkOfInterest = CreateNewWalkOfInterest(childNetworkHops);

            return Result.Ok((newWalkOfInterest, utilityNetworkHopsToUpdateByIndex));
        }

        private IEnumerable<Guid> GetElementsExceptFirst(RouteNetworkElementIdList routeNetworkElementRefs)
        {
            List<Guid> result = new();

            bool first = true;

            foreach (var routeNetworkElementRef in routeNetworkElementRefs)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    result.Add(routeNetworkElementRef);
                }
            }

            return result;
        }

        private IEnumerable<Guid> GetElementsExceptFirst(IEnumerable<Guid> routeNetworkElementRefs)
        {
            List<Guid> result = new();

            bool first = true;

            foreach (var routeNetworkElementRef in routeNetworkElementRefs)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    result.Add(routeNetworkElementRef);
                }
            }

            return result;
        }

        private bool IsAnyParentSubwalksMoved(ValidatedRouteNetworkWalk existingWalk, ValidatedRouteNetworkWalk newWalk)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.UtilityNetworkHops == null || _spanEquipment.UtilityNetworkHops.Count() == 0)
                return false;

            var hops = FindRouteNetworkHops(existingWalk);

            foreach (var hop in hops.Where(h => h.IsUtilityHop))
            {
                // If the walk of the utility hop is no longer present in the new walk, then the span equipment is moved outside where it is affixed
                if (!WalkContainsWalk(newWalk, hop.Walk))
                    return true;
            }

            return false;
        }

        private bool WalkContainsWalk(ValidatedRouteNetworkWalk walk, ValidatedRouteNetworkWalk walkToCheck)
        {
            int checkIndex = 0;

            bool startFound = false;

            foreach (var routeNetworkElement in walk.RouteNetworkElementRefs)
            {
                if (!startFound && routeNetworkElement == walkToCheck.RouteNetworkElementRefs.First())
                {
                    startFound = true;
                }

                if (startFound)
                {
                    if (checkIndex < walkToCheck.RouteNetworkElementRefs.Count())
                    {
                        if (routeNetworkElement != walkToCheck.RouteNetworkElementRefs[checkIndex])
                            return false;
                    }

                    checkIndex++;
                }
            }

            if (checkIndex < walkToCheck.RouteNetworkElementRefs.Count())
                return false;

            return true;
        }

        private Guid[] IntermediateCutNodeIds
        {
            get
            {
                if (_spanEquipment == null)
                    throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

                List<Guid> result = new List<Guid>();

                for (int i = 1; i < (_spanEquipment.NodesOfInterestIds.Length - 1); i++)
                {
                    result.Add(_spanEquipment.NodesOfInterestIds[i]);
                }

                return result.ToArray();
            }
        }

        private Guid[] CreateNewNodesOfInterestIdList(ValidatedRouteNetworkWalk newWalk)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            Guid[] result = new Guid[_spanEquipment.NodesOfInterestIds.Length];

            for (int i = 0; i < _spanEquipment.NodesOfInterestIds.Length; i++)
                result[i] = _spanEquipment.NodesOfInterestIds[i];

            result[0] = newWalk.FromNodeId;
            result[^1] = newWalk.ToNodeId;

            return result;
        }

        private bool IsAnySpanSegmentsConnectedInNode(Guid nodeId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            var nodeIndex = Array.IndexOf(_spanEquipment.NodesOfInterestIds, nodeId);

            if (nodeIndex < 0)
                throw new ApplicationException($"Cannot find node with id: {nodeId} in span equipment nodes of interest");

            foreach (var spanStructure in _spanEquipment.SpanStructures)
            {
                foreach (var spanSegment in spanStructure.SpanSegments)
                {
                    if (spanSegment.FromTerminalId != Guid.Empty && spanSegment.FromNodeOfInterestIndex == nodeIndex)
                        return true;

                    if (spanSegment.ToTerminalId != Guid.Empty && spanSegment.ToNodeOfInterestIndex == nodeIndex)
                        return true;
                }
            }

            return false;
        }

        private void Apply(SpanEquipmentMoved @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Merge
        public Result Merge(CommandContext cmdContext, Guid routeNodeId, SpanEquipment spanEquipmentToMergeWith)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.SpecificationId != spanEquipmentToMergeWith.SpecificationId)
            {
                return Result.Fail(new MergeSpanEquipmentError(
                        MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_OF_SPECIFICATION_MISMATCH,
                        $"Cannot merge span equipment: {_spanEquipment.Id} with span equipment: {spanEquipmentToMergeWith.Id} because their specification is not the same.")
                    );
            }

            // This span equipment must end in the route node specificed
            if (_spanEquipment.NodesOfInterestIds.First() != routeNodeId && _spanEquipment.NodesOfInterestIds.Last() != routeNodeId)
            {
                return Result.Fail(new MergeSpanEquipmentError(
                       MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_ENDS_ARE_NOT_COLOCATED_IN_ROUTE_NODE,
                       $"Cannot merge span equipment: {_spanEquipment.Id} with span equipment: {spanEquipmentToMergeWith.Id} because no ends of the former is ending in route node: {routeNodeId}")
                   );
            }

            // The other span equipment must end in the route node specificed as well
            if (spanEquipmentToMergeWith.NodesOfInterestIds.First() != routeNodeId && spanEquipmentToMergeWith.NodesOfInterestIds.Last() != routeNodeId)
            {
                return Result.Fail(new MergeSpanEquipmentError(
                       MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_ENDS_ARE_NOT_COLOCATED_IN_ROUTE_NODE,
                       $"Cannot merge span equipment: {_spanEquipment.Id} with span equipment: {spanEquipmentToMergeWith.Id} because no ends of the later is ending in route node: {routeNodeId}")
                   );
            }

            // The span equipment we want to merge with this one cannot contain any connections (because it's deleted)
            if (spanEquipmentToMergeWith.SpanStructures.Any(st => st.SpanSegments.Any(se => se.FromTerminalId != Guid.Empty || se.ToTerminalId != Guid.Empty)))
            {
                return Result.Fail(new MergeSpanEquipmentError(
                        MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_OF_CONNECTIVITY,
                        $"Cannot merge span equipment: {_spanEquipment.Id} with span equipment: {spanEquipmentToMergeWith.Id} because the later has connectivity to other span segments.")
                    );
            }

            // This span equipment cannot be affixed to node container in merge node
            if (_spanEquipment.NodeContainerAffixes != null && _spanEquipment.NodeContainerAffixes.Any(affix => affix.RouteNodeId == routeNodeId))
            {
                return Result.Fail(new MergeSpanEquipmentError(
                        MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_END_IS_AFFIXED_TO_NODE_CONTAINER,
                        $"Cannot merge span equipment: {_spanEquipment.Id} with span equipment: {spanEquipmentToMergeWith.Id} because the former is affixed to a node container in route node: {routeNodeId}")
                    );
            }

            // The span equipment we want to merge with cannot be affixed to node container in merge node
            if (spanEquipmentToMergeWith.NodeContainerAffixes != null && spanEquipmentToMergeWith.NodeContainerAffixes.Any(affix => affix.RouteNodeId == routeNodeId))
            {
                return Result.Fail(new MergeSpanEquipmentError(
                        MergeSpanEquipmentErrorCodes.CANNOT_MERGE_SPAN_EQUIPMENT_BECAUSE_END_IS_AFFIXED_TO_NODE_CONTAINER,
                        $"Cannot merge span equipment: {_spanEquipment.Id} with span equipment: {spanEquipmentToMergeWith.Id} because the later is affixed to a node container in route node: {routeNodeId}")
                    );
            }


            // Create a new node of interest id array that include the new end from the other span equipment resulting from the merge
            Guid[] updatedNodeOfInterestIds = new Guid[_spanEquipment.NodesOfInterestIds.Length];
            _spanEquipment.NodesOfInterestIds.CopyTo(updatedNodeOfInterestIds, 0);

            if (_spanEquipment.NodesOfInterestIds.First() == spanEquipmentToMergeWith.NodesOfInterestIds.First())
                updatedNodeOfInterestIds[0] = spanEquipmentToMergeWith.NodesOfInterestIds.Last();
            else if (_spanEquipment.NodesOfInterestIds.Last() == spanEquipmentToMergeWith.NodesOfInterestIds.First())
                updatedNodeOfInterestIds[^1] = spanEquipmentToMergeWith.NodesOfInterestIds.Last();
            else if (_spanEquipment.NodesOfInterestIds.First() == spanEquipmentToMergeWith.NodesOfInterestIds.Last())
                updatedNodeOfInterestIds[0] = spanEquipmentToMergeWith.NodesOfInterestIds.First();
            else if (_spanEquipment.NodesOfInterestIds.Last() == spanEquipmentToMergeWith.NodesOfInterestIds.Last())
                updatedNodeOfInterestIds[^1] = spanEquipmentToMergeWith.NodesOfInterestIds.First();
            else
                throw new ApplicationException("There an unexpected error in shared node validation and/or identification");


            var @event = new SpanEquipmentMerged(
              spanEquipmentId: this.Id,
              nodesOfInterestIds: updatedNodeOfInterestIds
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanEquipmentMerged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Change Naming Info
        public Result ChangeNamingInfo(CommandContext cmdContext, NamingInfo? namingInfo)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that equipment has never been placed. Please check command handler logic.");

            if ((_spanEquipment.NamingInfo == null && namingInfo == null) || (_spanEquipment.NamingInfo != null && _spanEquipment.NamingInfo.Equals(namingInfo)))
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_NAMING_INFO,
                       $"Will not update naming info, because the provided value is equal the existing value.")
                   );
            }

            var @event = new SpanEquipmentNamingInfoChanged(
              spanEquipmentId: this.Id,
              namingInfo: namingInfo
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanEquipmentNamingInfoChanged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Change Marking Info
        public Result ChangeMarkingInfo(CommandContext cmdContext, MarkingInfo? markingInfo)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if ((_spanEquipment.MarkingInfo == null && markingInfo == null) || (_spanEquipment.MarkingInfo != null && _spanEquipment.MarkingInfo.Equals(markingInfo)))
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_MARKING_INFO,
                       $"Will not update marking info, because the provided value is equal the existing value.")
                   );
            }

            var @event = new SpanEquipmentMarkingInfoChanged(
              spanEquipmentId: this.Id,
              markingInfo: markingInfo
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanEquipmentMarkingInfoChanged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Change Address Info
        public Result ChangeAddressInfo(CommandContext cmdContext, AddressInfo? addressInfo)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if ((_spanEquipment.AddressInfo == null && addressInfo == null) || (_spanEquipment.AddressInfo != null && _spanEquipment.AddressInfo.Equals(addressInfo)))
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_ADDRESS_INFO,
                       $"Will not update address info, because the provided value is equal the existing value.")
                   );
            }

            var @event = new SpanEquipmentAddressInfoChanged(
              spanEquipmentId: this.Id,
              addressInfo: addressInfo
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanEquipmentAddressInfoChanged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Change Manufacturer
        public Result ChangeManufacturer(CommandContext cmdContext, Guid manufacturerId)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.ManufacturerId == manufacturerId)
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_MANUFACTURER,
                       $"Will not change manufacturer, because the provided value is equal the existing value.")
                   );
            }

            var @event = new SpanEquipmentManufacturerChanged(
              spanEquipmentId: this.Id,
              manufacturerId: manufacturerId
            )
            {
                CorrelationId = cmdContext.CorrelationId,
                IncitingCmdId = cmdContext.CmdId,
                UserName = cmdContext.UserContext?.UserName,
                WorkTaskId = cmdContext.UserContext?.WorkTaskId
            };

            RaiseEvent(@event);

            return Result.Ok();
        }

        private void Apply(SpanEquipmentManufacturerChanged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        #region Change Specification
        public Result ChangeSpecification(CommandContext cmdContext, SpanEquipmentSpecification currentSpecification, SpanEquipmentSpecification newSpecification)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            if (_spanEquipment.SpecificationId == newSpecification.Id)
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.NO_CHANGE_TO_SPECIFICATION,
                       $"Will not change specification, because the provided specification id is the same as the existing one.")
                   );
            }

            // Changed specification from non-fixed to fixed is not allowedd
            if (!currentSpecification.IsFixed && newSpecification.IsFixed)
            {
                return Result.Fail(new UpdateEquipmentPropertiesError(
                       UpdateEquipmentPropertiesErrorCodes.CANNOT_CHANGE_FROM_NON_FIXED_TO_FIXED,
                       $"No rules defined that can handle a change from a non-fixed multi span equipment to a fixed one.")
                   );
            }

            // If specification changed from is-fixed to non-fixed or non-fixed to another non-fixed, we just need to update the span equipment specification id and outer structure specification id
            if ((currentSpecification.IsFixed && !newSpecification.IsFixed) || (!currentSpecification.IsFixed && !newSpecification.IsFixed))
            {
                StructureModificationInstruction updateOuterSpanStructureInstruction = new StructureModificationInstruction(_spanEquipment.SpanStructures[0].Id, false)
                {
                    StructureSpecificationIdToBeUpdated = newSpecification.RootTemplate.SpanStructureSpecificationId
                };


                // Check that no segments are connected in existing structure, because they will be removed when changed to a non fixed (empty
                for (int existingStructureIndex = 0; existingStructureIndex < _spanEquipment.SpanStructures.Length; existingStructureIndex++)
                {
                    var existingStructure = _spanEquipment.SpanStructures[existingStructureIndex];

                    if (IsAnySpanSegmentsInStructureConnected((ushort)existingStructureIndex))
                    {
                        return Result.Fail(new UpdateEquipmentPropertiesError(
                            UpdateEquipmentPropertiesErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURE_WITH_CONNECTED_SEGMENTS_FROM_SPAN_EQUIPMENT,
                            $"The new specification contains less span structure that the old one. But cannot remove span structure at index: {existingStructureIndex} because some of its segments are connected.")
                        );
                    }
                }

                var @event = new SpanEquipmentSpecificationChanged(
                  spanEquipmentId: this.Id,
                  newSpecificationId: newSpecification.Id,
                  structureModificationInstructions: new StructureModificationInstruction[] { updateOuterSpanStructureInstruction }
                )
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                };

                RaiseEvent(@event);

                return Result.Ok();
            }
           

            // If the specification is changed from fixed to another fixed span equipment then the fun begins
            if (currentSpecification.IsFixed && newSpecification.IsFixed)
            {
                List<StructureModificationInstruction> structureModificationInstructions = new List<StructureModificationInstruction>();

                // For all structures that exists in the existing span equipment that is also present in the new specification, update the specification id
                var newSpecificationStructureTemplates = newSpecification.RootTemplate.GetAllSpanStructureTemplatesRecursive();

                HashSet<SpanStructure> existingStructureProcessed = new();
                HashSet<SpanStructureTemplate> newSpecificationSpanStructureTemplateProcessed = new();

                foreach (var existingStructure in _spanEquipment.SpanStructures)
                {
                    var newStructureTemplate = newSpecificationStructureTemplates.FirstOrDefault(n => n.Level == existingStructure.Level && n.Position == existingStructure.Position);

                    if (newStructureTemplate != null)
                    {
                        var updateStructureSpecIdInstruction = new StructureModificationInstruction(existingStructure.Id, false)
                        {
                            StructureSpecificationIdToBeUpdated = newStructureTemplate.SpanStructureSpecificationId
                        };

                        structureModificationInstructions.Add(updateStructureSpecIdInstruction);

                        newSpecificationSpanStructureTemplateProcessed.Add(newStructureTemplate);
                        existingStructureProcessed.Add(existingStructure);
                    }
                }

                // For all structures in the existing span equipment that was not in the new specification, check if can be deleted and if so create instruction for that
                for (int existingStructureIndex = 0; existingStructureIndex < _spanEquipment.SpanStructures.Length; existingStructureIndex++)
                {
                    var existingStructure = _spanEquipment.SpanStructures[existingStructureIndex];

                    if (!existingStructureProcessed.Contains(existingStructure))
                    {
                        if (IsAnySpanSegmentsInStructureConnected((ushort)existingStructureIndex))
                        {
                            return Result.Fail(new UpdateEquipmentPropertiesError(
                                UpdateEquipmentPropertiesErrorCodes.CANNOT_REMOVE_SPAN_STRUCTURE_WITH_CONNECTED_SEGMENTS_FROM_SPAN_EQUIPMENT,
                                $"The new specification contains less span structure that the old one. But cannot remove span structure at index: {existingStructureIndex} because some of its segments are connected.")
                            );
                        }

                        var deleteStructureSpecIdInstruction = new StructureModificationInstruction(existingStructure.Id, true);
                        structureModificationInstructions.Add(deleteStructureSpecIdInstruction);

                        existingStructureProcessed.Add(existingStructure);
                    }
                }

                // Finnally add level two structures pressent in new specification that was not pressent in the existing span equipment
                foreach (var spanStructureTemplate in newSpecification.RootTemplate.ChildTemplates)
                {
                    if (!newSpecificationSpanStructureTemplateProcessed.Contains(spanStructureTemplate))
                    {
                        var newStructure = new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: spanStructureTemplate.SpanStructureSpecificationId,
                            level: spanStructureTemplate.Level,
                            position: spanStructureTemplate.Position,
                            parentPosition: 1,
                            spanSegments: new SpanSegment[] { new SpanSegment(Guid.NewGuid(), 0, (ushort)(_spanEquipment.NodesOfInterestIds.Length - 1)) }
                        );

                        var addStructureSpecIdInstruction = new StructureModificationInstruction(newStructure.Id, false)
                        {
                            NewStructureToBeInserted = newStructure
                        };
                        
                        structureModificationInstructions.Add(addStructureSpecIdInstruction);

                        newSpecificationSpanStructureTemplateProcessed.Add(spanStructureTemplate);
                    }
                }

                var @event = new SpanEquipmentSpecificationChanged(
                  spanEquipmentId: this.Id,
                  newSpecificationId: newSpecification.Id,
                  structureModificationInstructions: structureModificationInstructions.ToArray()
                )
                {
                    CorrelationId = cmdContext.CorrelationId,
                    IncitingCmdId = cmdContext.CmdId,
                    UserName = cmdContext.UserContext?.UserName,
                    WorkTaskId = cmdContext.UserContext?.WorkTaskId
                };

                RaiseEvent(@event);

                return Result.Ok();
            }

            throw new ApplicationException("Specification change not supported. Check change span equipment specification logic.");
        }

        private void Apply(SpanEquipmentSpecificationChanged @event)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            _spanEquipment = SpanEquipmentProjectionFunctions.Apply(_spanEquipment, @event);
        }

        #endregion

        private List<ExistingRouteHop> FindRouteNetworkHops(ValidatedRouteNetworkWalk walkOfInterest)
        {
            if (_spanEquipment == null)
                throw new ApplicationException($"Invalid internal state. Span equipment property cannot be null. Seems that span equipment has never been placed. Please check command handler logic.");

            Stack<UtilityNetworkHop> hops = _spanEquipment.UtilityNetworkHops != null ? new(_spanEquipment.UtilityNetworkHops.Reverse()) : new();

            List<ExistingRouteHop> result = new();

            int hopSequenceNumber = 0;

            var insideHop = false;

            List<Guid> routeNetworkHoleIds = new List<Guid>();

            List<Guid> utilityHopIds = new List<Guid>();

            int routeNetworkElementIndex = 0;

            int latestFromIndex = 0;

            foreach (var routeElementId in walkOfInterest.RouteNetworkElementRefs)
            {
                if (hops.Count > 0)
                {
                    var hop = hops.Peek();

                    // We hit a hop
                    if (hop.FromNodeId == routeElementId)
                    {
                        insideHop = true;

                        utilityHopIds.Add(routeElementId);

                        if (routeNetworkHoleIds.Count > 0)
                        {
                            var idList = new RouteNetworkElementIdList();

                            idList.AddRange(routeNetworkHoleIds);
                            idList.Add(routeElementId);

                            result.Add(new ExistingRouteHop() { SequenceNumber = hopSequenceNumber, FromWalkOfInterestIndex = latestFromIndex, ToWalkOfInterestIndex = routeNetworkElementIndex, IsUtilityHop = false, Walk = new ValidatedRouteNetworkWalk(idList) });
                            hopSequenceNumber++;
                            routeNetworkHoleIds.Clear();
                        }

                        latestFromIndex = routeNetworkElementIndex;
                    }
                    else if (hop.ToNodeId == routeElementId)
                    {
                        // We hit an end node, so we not inside the hop anymore, so we pop it
                        insideHop = false;
                        var utilityNetworkHop = hops.Pop();

                        // Create utility route hop
                        var idList = new RouteNetworkElementIdList();
                        idList.AddRange(utilityHopIds);
                        idList.Add(routeElementId);

                        result.Add(new ExistingRouteHop() { SequenceNumber = hopSequenceNumber, FromWalkOfInterestIndex = latestFromIndex, ToWalkOfInterestIndex = routeNetworkElementIndex, IsUtilityHop = true, Walk = new ValidatedRouteNetworkWalk(idList), UtilityNetworkHop = utilityNetworkHop });
                        hopSequenceNumber++;
                        latestFromIndex = routeNetworkElementIndex;
                        utilityHopIds.Clear();

                        // Check if we hit next hop
                        if (hops.Count != 0 && hops.Peek().FromNodeId == routeElementId)
                        {
                            insideHop = true;
                            utilityHopIds.Add(routeElementId);
                        }
                        else
                        {
                            routeNetworkHoleIds.Add(routeElementId);
                        }
                    }
                    else
                    {
                        // If we're not inside a hop (i.e we are in the trench) then we add the node
                        if (!insideHop)
                            routeNetworkHoleIds.Add(routeElementId);
                        else
                            utilityHopIds.Add(routeElementId);

                    }
                }
                else
                {
                    routeNetworkHoleIds.Add(routeElementId);
                }

                routeNetworkElementIndex++;
            }

            if (routeNetworkHoleIds.Count > 2)
            {
                var idList = new RouteNetworkElementIdList();

                idList.AddRange(routeNetworkHoleIds);
                result.Add(new ExistingRouteHop() { SequenceNumber = hopSequenceNumber, FromWalkOfInterestIndex = latestFromIndex, ToWalkOfInterestIndex = (routeNetworkElementIndex - 1), IsUtilityHop = false, Walk = new ValidatedRouteNetworkWalk(idList) });
                hopSequenceNumber++;

                routeNetworkHoleIds.Clear();
            }

            result.First().IsFirst = true;
            result.Last().IsLast = true;

            return result;
        }

     
        private class ExistingRouteHop
        {
            public int SequenceNumber { get; set; }
            public int FromWalkOfInterestIndex { get; set; }
            public int ToWalkOfInterestIndex { get; set; }

            public bool IsUtilityHop { get; set; }
            public UtilityNetworkHop UtilityNetworkHop { get; set; }
            public ValidatedRouteNetworkWalk Walk { get; set; }

            public bool IsFirst;
            public bool IsLast;
        }
    }
}
