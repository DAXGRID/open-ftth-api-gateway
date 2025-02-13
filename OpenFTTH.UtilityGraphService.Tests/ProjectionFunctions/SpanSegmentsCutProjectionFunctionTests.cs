using FluentAssertions;
using OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork;
using OpenFTTH.UtilityGraphService.Business.Graph.Projections;
using OpenFTTH.UtilityGraphService.Business.SpanEquipments.Events;
using System;
using Xunit;

namespace OpenFTTH.UtilityGraphService.Tests.ProjectionFunctions
{
    public class SpanSegmentsCutProjectionFunctionTests
    {
        [Fact]
        public void TestCutStructureOneTime_ShouldSucceed()
        {
            // Setup
            var cutNodeOfInterestId = Guid.NewGuid();

            var spanSegmentId1ToCut = Guid.NewGuid();
            var spanSegmentId2 = Guid.NewGuid();

            var existingSpanEquipment = new OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.SpanEquipment(
                id: Guid.NewGuid(),
                specificationId: Guid.NewGuid(),
                walkOfInterestId: Guid.NewGuid(),
                nodesOfInterestIds: new Guid[] { Guid.NewGuid(), Guid.NewGuid() },
                spanStructures: 
                    new SpanStructure[]
                    {
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 1,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(spanSegmentId1ToCut, 0, 1)
                                }
                        ),
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 2,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(spanSegmentId2, 0, 1)
                                }
                        )
                    }
            );

            // Cut first structure
            var newSegmentId1 = Guid.NewGuid();
            var newSegmentId2 = Guid.NewGuid();

            var cutEvent = new SpanSegmentsCut(
                spanEquipmentId: existingSpanEquipment.Id,
                cutNodeOfInterestId: cutNodeOfInterestId,
                cutNodeOfInterestIndex: 1,
                cuts:
                    new SpanSegmentCutInfo[]
                    {
                        new SpanSegmentCutInfo(
                            oldSpanSegmentId: spanSegmentId1ToCut,
                            newSpanSegmentId1: newSegmentId1,
                            newSpanSegmentId2: newSegmentId2
                            )
                    }
             );

            // Act
            var newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, cutEvent);

            // Assert
            newSpanEquipment.NodesOfInterestIds.Length.Should().Be(3);

            // Check that the cut node is inserted at index 1
            newSpanEquipment.NodesOfInterestIds[1].Should().Be(cutNodeOfInterestId);

            // Check that structure 1 has been cut correctly
            newSpanEquipment.SpanStructures[0].SpanSegments.Length.Should().Be(2);

            newSpanEquipment.SpanStructures[0].SpanSegments[0].Id.Should().Be(newSegmentId1);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);

            newSpanEquipment.SpanStructures[0].SpanSegments[1].Id.Should().Be(newSegmentId2);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(1);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(2);

            // Check that structure 2 has got new node of interest index set
            newSpanEquipment.SpanStructures[1].SpanSegments.Length.Should().Be(1);
            newSpanEquipment.SpanStructures[1].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[1].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(2);
        }

        [Fact]
        public void TestCutSameStructureMultipleTimeAtDifferentNodes_ShouldSucceed()
        {
            // Setup
            var cut1spanSegmentId = Guid.NewGuid();
            var cut1newSegmentId1 = Guid.NewGuid();
            var cut1newSegmentId2 = Guid.NewGuid();

            var existingSpanEquipment = new OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.SpanEquipment(
                id: Guid.NewGuid(),
                specificationId: Guid.NewGuid(),
                walkOfInterestId: Guid.NewGuid(),
                nodesOfInterestIds: new Guid[] { Guid.NewGuid(), Guid.NewGuid() },
                spanStructures:
                    new SpanStructure[]
                    {
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 1,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(cut1spanSegmentId, 0, 1)
                                }
                        )
                    }
            );

            // Cut first time
            var cut1Event = new SpanSegmentsCut(
                spanEquipmentId: existingSpanEquipment.Id,
                cutNodeOfInterestId: Guid.NewGuid(),
                cutNodeOfInterestIndex: 1,
                cuts:
                    new SpanSegmentCutInfo[]
                    {
                        new SpanSegmentCutInfo(
                            oldSpanSegmentId: cut1spanSegmentId,
                            newSpanSegmentId1: cut1newSegmentId1,
                            newSpanSegmentId2: cut1newSegmentId2
                            )
                    }
             );

            var cut2newSegmentId1 = Guid.NewGuid();
            var cut2newSegmentId2 = Guid.NewGuid();

            // Cut segment 1 from first cut
            var cut2Event = new SpanSegmentsCut(
                 spanEquipmentId: existingSpanEquipment.Id,
                 cutNodeOfInterestId: Guid.NewGuid(),
                 cutNodeOfInterestIndex: 1,
                 cuts:
                     new SpanSegmentCutInfo[]
                     {
                                    new SpanSegmentCutInfo(
                                        oldSpanSegmentId: cut1newSegmentId1,
                                        newSpanSegmentId1: cut2newSegmentId1,
                                        newSpanSegmentId2: cut2newSegmentId2
                                        )
                     }
              );

            // Act
            var newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, cut1Event);
            newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(newSpanEquipment, cut2Event);

            // Assert
            newSpanEquipment.NodesOfInterestIds.Length.Should().Be(4);
            newSpanEquipment.SpanStructures[0].SpanSegments.Length.Should().Be(3);

            newSpanEquipment.SpanStructures[0].SpanSegments[0].Id.Should().Be(cut2newSegmentId1);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);

            newSpanEquipment.SpanStructures[0].SpanSegments[1].Id.Should().Be(cut2newSegmentId2);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(1);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(2);

            newSpanEquipment.SpanStructures[0].SpanSegments[2].Id.Should().Be(cut1newSegmentId2);
            newSpanEquipment.SpanStructures[0].SpanSegments[2].FromNodeOfInterestIndex.Should().Be(2);
            newSpanEquipment.SpanStructures[0].SpanSegments[2].ToNodeOfInterestIndex.Should().Be(3);
        }

        [Fact]
        public void TestCutMultipleStructuresMultipleTimeAtDifferentNodes_ShouldSucceed()
        {
            // Setup
            var structure1cut1OldSpanSegmentId = Guid.NewGuid();
            var structure2cut1OldSpanSegmentId = Guid.NewGuid();

            var existingSpanEquipment = new OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.SpanEquipment(
                id: Guid.NewGuid(),
                specificationId: Guid.NewGuid(),
                walkOfInterestId: Guid.NewGuid(),
                nodesOfInterestIds: new Guid[] { Guid.NewGuid(), Guid.NewGuid() },
                spanStructures:
                    new SpanStructure[]
                    {
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 1,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(structure1cut1OldSpanSegmentId, 0, 1)
                                }
                        ),
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 2,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(structure2cut1OldSpanSegmentId, 0, 1)
                                }
                        )
                    }
            );

            // Cut structure 1 first time
            var structure1cut1newSegmentId1 = Guid.NewGuid();
            var structure1cut1newSegmentId2 = Guid.NewGuid();

            var cut1Event = new SpanSegmentsCut(
                spanEquipmentId: existingSpanEquipment.Id,
                cutNodeOfInterestId: Guid.NewGuid(),
                cutNodeOfInterestIndex: 1,
                cuts:
                    new SpanSegmentCutInfo[]
                    {
                        new SpanSegmentCutInfo(
                            oldSpanSegmentId: structure1cut1OldSpanSegmentId,
                            newSpanSegmentId1: structure1cut1newSegmentId1,
                            newSpanSegmentId2: structure1cut1newSegmentId2
                            )
                    }
            );

            // Cut structure 2 second time
            var structure1cut2newSegmentId1 = Guid.NewGuid();
            var structure1cut2newSegmentId2 = Guid.NewGuid();
            
            var cut2Event = new SpanSegmentsCut(
                 spanEquipmentId: existingSpanEquipment.Id,
                 cutNodeOfInterestId: Guid.NewGuid(),
                 cutNodeOfInterestIndex: 1,
                 cuts:
                     new SpanSegmentCutInfo[]
                     {
                                    new SpanSegmentCutInfo(
                                        oldSpanSegmentId: structure1cut1newSegmentId1,
                                        newSpanSegmentId1: structure1cut2newSegmentId1,
                                        newSpanSegmentId2: structure1cut2newSegmentId2
                                        )
                     }
            );

            // Cut structure 1 first time
            var structure2cut1newSegmentId1 = Guid.NewGuid();
            var structure2cut1newSegmentId2 = Guid.NewGuid();

            var cut3Event = new SpanSegmentsCut(
                 spanEquipmentId: existingSpanEquipment.Id,
                 cutNodeOfInterestId: Guid.NewGuid(),
                 cutNodeOfInterestIndex: 1,
                 cuts:
                     new SpanSegmentCutInfo[]
                     {
                                    new SpanSegmentCutInfo(
                                        oldSpanSegmentId: structure2cut1OldSpanSegmentId,
                                        newSpanSegmentId1: structure2cut1newSegmentId1,
                                        newSpanSegmentId2: structure2cut1newSegmentId2
                                        )
                     }
            );

            // Act
            var newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, cut1Event);
            newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(newSpanEquipment, cut2Event);
            newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(newSpanEquipment, cut3Event);

            // Assert
            newSpanEquipment.NodesOfInterestIds.Length.Should().Be(5);
            newSpanEquipment.NodesOfInterestIds[0].Should().Be(existingSpanEquipment.NodesOfInterestIds[0]);
            newSpanEquipment.NodesOfInterestIds[1].Should().Be(cut3Event.CutNodeOfInterestId);
            newSpanEquipment.NodesOfInterestIds[2].Should().Be(cut2Event.CutNodeOfInterestId);
            newSpanEquipment.NodesOfInterestIds[3].Should().Be(cut1Event.CutNodeOfInterestId);
            newSpanEquipment.NodesOfInterestIds[4].Should().Be(existingSpanEquipment.NodesOfInterestIds[1]);

            // Structure 1
            newSpanEquipment.SpanStructures[0].SpanSegments.Length.Should().Be(3);

            newSpanEquipment.SpanStructures[0].SpanSegments[0].Id.Should().Be(structure1cut2newSegmentId1);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(2);

            newSpanEquipment.SpanStructures[0].SpanSegments[1].Id.Should().Be(structure1cut2newSegmentId2);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(2);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(3);

            newSpanEquipment.SpanStructures[0].SpanSegments[2].Id.Should().Be(structure1cut1newSegmentId2);
            newSpanEquipment.SpanStructures[0].SpanSegments[2].FromNodeOfInterestIndex.Should().Be(3);
            newSpanEquipment.SpanStructures[0].SpanSegments[2].ToNodeOfInterestIndex.Should().Be(4);

            // Structure 2
            newSpanEquipment.SpanStructures[1].SpanSegments.Length.Should().Be(2);

            newSpanEquipment.SpanStructures[1].SpanSegments[0].Id.Should().Be(structure2cut1newSegmentId1);
            newSpanEquipment.SpanStructures[1].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[1].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);

            newSpanEquipment.SpanStructures[1].SpanSegments[1].Id.Should().Be(structure2cut1newSegmentId2);
            newSpanEquipment.SpanStructures[1].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(1);
            newSpanEquipment.SpanStructures[1].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(4);
        }

        [Fact]
        public void TestCutTwoStructuresAtSameNode_ShouldSucceed()
        {
            // Setup
            var cutNodeOfInterestId = Guid.NewGuid();
            var cut1spanSegmentId = Guid.NewGuid();
            var cut2spanSegmentId = Guid.NewGuid();

            var existingSpanEquipment = new OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.SpanEquipment(
                id: Guid.NewGuid(),
                specificationId: Guid.NewGuid(),
                walkOfInterestId: Guid.NewGuid(),
                nodesOfInterestIds: new Guid[] { Guid.NewGuid(), Guid.NewGuid() },
                spanStructures:
                    new SpanStructure[]
                    {
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 1,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(cut1spanSegmentId, 0, 1)
                                }
                        ),
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 2,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(cut2spanSegmentId, 0, 1)
                                }
                        )
                    }
            );

            // Cut first structure
            var cut1newSegmentId1 = Guid.NewGuid();
            var cut1newSegmentId2 = Guid.NewGuid();

            var cut1Event = new SpanSegmentsCut(
                spanEquipmentId: existingSpanEquipment.Id,
                cutNodeOfInterestId: cutNodeOfInterestId,
                cutNodeOfInterestIndex: 1,
                cuts:
                    new SpanSegmentCutInfo[]
                    {
                        new SpanSegmentCutInfo(
                            oldSpanSegmentId: cut1spanSegmentId,
                            newSpanSegmentId1: cut1newSegmentId1,
                            newSpanSegmentId2: cut1newSegmentId2
                            )
                    }
             );


            // Cut second structure
            var cut2newSegmentId1 = Guid.NewGuid();
            var cut2newSegmentId2 = Guid.NewGuid();
                        
            var cut2Event = new SpanSegmentsCut(
                 spanEquipmentId: existingSpanEquipment.Id,
                 cutNodeOfInterestId: cutNodeOfInterestId,
                 cutNodeOfInterestIndex: 1,
                 cuts:
                     new SpanSegmentCutInfo[]
                     {
                                    new SpanSegmentCutInfo(
                                        oldSpanSegmentId: cut2spanSegmentId,
                                        newSpanSegmentId1: cut2newSegmentId1,
                                        newSpanSegmentId2: cut2newSegmentId2
                                        )
                     }
              );

            // Act
            var newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, cut1Event);
            newSpanEquipment = SpanEquipmentProjectionFunctions.Apply(newSpanEquipment, cut2Event);

            // Assert
            newSpanEquipment.NodesOfInterestIds.Length.Should().Be(3);
            newSpanEquipment.SpanStructures[0].SpanSegments.Length.Should().Be(2);

            // Structure 1
            newSpanEquipment.SpanStructures[0].SpanSegments[0].Id.Should().Be(cut1newSegmentId1);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[0].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);

            newSpanEquipment.SpanStructures[0].SpanSegments[1].Id.Should().Be(cut1newSegmentId2);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(1);
            newSpanEquipment.SpanStructures[0].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(2);

            // Structure 2
            newSpanEquipment.SpanStructures[1].SpanSegments[0].Id.Should().Be(cut2newSegmentId1);
            newSpanEquipment.SpanStructures[1].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment.SpanStructures[1].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);

            newSpanEquipment.SpanStructures[1].SpanSegments[1].Id.Should().Be(cut2newSegmentId2);
            newSpanEquipment.SpanStructures[1].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(1);
            newSpanEquipment.SpanStructures[1].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(2);
        }

        [Fact]
        public void TestCutDifferentStructureMultipleTime_ShouldSucceed()
        {
            // Setup
            var cut1spanSegmentId = Guid.NewGuid();
            var cut2spanSegmentId = Guid.NewGuid();
            var cut3spanSegmentId = Guid.NewGuid();

            var existingSpanEquipment = new OpenFTTH.UtilityGraphService.API.Model.UtilityNetwork.SpanEquipment(
                id: Guid.NewGuid(),
                specificationId: Guid.NewGuid(),
                walkOfInterestId: Guid.NewGuid(),
                nodesOfInterestIds: new Guid[] { Guid.NewGuid(), Guid.NewGuid() },
                spanStructures:
                    new SpanStructure[]
                    {
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 1,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(cut1spanSegmentId, 0, 1)
                                }
                        ),
                        new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 2,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(cut2spanSegmentId, 0, 1)
                                }
                        ),
                         new SpanStructure(
                            id: Guid.NewGuid(),
                            specificationId: Guid.NewGuid(),
                            level: 1,
                            position: 3,
                            parentPosition: 0,
                            spanSegments:
                                new SpanSegment[]
                                {
                                    new SpanSegment(cut3spanSegmentId, 0, 1)
                                }
                        )
                    }
            );

            // Cut first structure
            var cut1newSegmentId1 = Guid.NewGuid();
            var cut1newSegmentId2 = Guid.NewGuid();

            var cutEvent1 = new SpanSegmentsCut(
                spanEquipmentId: existingSpanEquipment.Id,
                cutNodeOfInterestId: Guid.NewGuid(),
                cutNodeOfInterestIndex: 1,
                cuts:
                    new SpanSegmentCutInfo[]
                    {
                        new SpanSegmentCutInfo(
                            oldSpanSegmentId: cut1spanSegmentId,
                            newSpanSegmentId1: cut1newSegmentId1,
                            newSpanSegmentId2: cut1newSegmentId2
                            )
                    }
             );

            var newSpanEquipment1 = SpanEquipmentProjectionFunctions.Apply(existingSpanEquipment, cutEvent1);

            // Cut second structure
            var cut2newSegmentId1 = Guid.NewGuid();
            var cut2newSegmentId2 = Guid.NewGuid();

            var cutEvent2 = new SpanSegmentsCut(
                spanEquipmentId: existingSpanEquipment.Id,
                cutNodeOfInterestId: Guid.NewGuid(),
                cutNodeOfInterestIndex: 2,
                cuts:
                    new SpanSegmentCutInfo[]
                    {
                        new SpanSegmentCutInfo(
                            oldSpanSegmentId: cut1spanSegmentId,
                            newSpanSegmentId1: cut1newSegmentId1,
                            newSpanSegmentId2: cut1newSegmentId2
                            )
                    }
             );

            var newSpanEquipment2 = SpanEquipmentProjectionFunctions.Apply(newSpanEquipment1, cutEvent2);



            newSpanEquipment1.NodesOfInterestIds.Length.Should().Be(3);

            // Check that structure 1 has been cut correctly
            newSpanEquipment1.SpanStructures[0].SpanSegments.Length.Should().Be(2);

            newSpanEquipment1.SpanStructures[0].SpanSegments[0].Id.Should().Be(cut1newSegmentId1);
            newSpanEquipment1.SpanStructures[0].SpanSegments[0].FromNodeOfInterestIndex.Should().Be(0);
            newSpanEquipment1.SpanStructures[0].SpanSegments[0].ToNodeOfInterestIndex.Should().Be(1);

            newSpanEquipment1.SpanStructures[0].SpanSegments[1].Id.Should().Be(cut1newSegmentId2);
            newSpanEquipment1.SpanStructures[0].SpanSegments[1].FromNodeOfInterestIndex.Should().Be(1);
            newSpanEquipment1.SpanStructures[0].SpanSegments[1].ToNodeOfInterestIndex.Should().Be(2);

       

        }
    }
}

