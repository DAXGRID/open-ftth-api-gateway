using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Business.StateHandling.Interest;
using System;
using Xunit;

namespace OpenFTTH.RouteNetwork.Tests
{
    public class InterestIndexTests
    {
        [Fact]
        public void TestIndexingSingleWalkOfInterest()
        {
            // Create a walk of interest spanning 3 route elements out of 4
            InMemInterestRelationIndex index = new InMemInterestRelationIndex();

            var routeElement1 = Guid.NewGuid();
            var routeElement2 = Guid.NewGuid();
            var routeElement3 = Guid.NewGuid();
            var routeElement4 = Guid.NewGuid();

            var walkIds = new RouteNetworkElementIdList() { routeElement2, routeElement3, routeElement4 };

            var walkOfInterest = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, walkIds);

            index.Add(walkOfInterest);

            // Act
            var routeElement1InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement1);
            var routeElement2InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement2);
            var routeElement3InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement3);
            var routeElement4InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement4);

            // Assert

            // Route element 1 has no relation to the interest
            Assert.Empty(routeElement1InterestRelations);

            // Route element 2 has a start relation to the interest
            Assert.Single(routeElement2InterestRelations);
            Assert.Equal(walkOfInterest.Id, routeElement2InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.Start, routeElement2InterestRelations[0].Item2);

            // Route element 3 has a passthroug relation to the interest
            Assert.Single(routeElement3InterestRelations);
            Assert.Equal(walkOfInterest.Id, routeElement3InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.PassThrough, routeElement3InterestRelations[0].Item2);

            // Route element 4 has an end relation to the interest
            Assert.Single(routeElement4InterestRelations);
            Assert.Equal(walkOfInterest.Id, routeElement4InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.End, routeElement4InterestRelations[0].Item2);
        }

        [Fact]
        public void TestIndexingSingleNodeOfInterest()
        {
            // Create a walk of interest spanning 3 route elements out of 4
            InMemInterestRelationIndex index = new InMemInterestRelationIndex();

            var routeElement1 = Guid.NewGuid();
            var routeElement2 = Guid.NewGuid();

            var nodeOfInterest = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.NodeOfInterest, new RouteNetworkElementIdList() { routeElement2 });

            // Act
            index.Add(nodeOfInterest);

            var routeElement1InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement1);
            var routeElement2InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement2);

            // Assert

            // Route element 1 has no relation to the interest
            Assert.Empty(routeElement1InterestRelations);

            // Route element 2 has an inside node relation to the interest
            Assert.Single(routeElement2InterestRelations);
            Assert.Equal(nodeOfInterest.Id, routeElement2InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.InsideNode, routeElement2InterestRelations[0].Item2);
        }


        [Fact]
        public void TestIndexingMultipleWalkOfInterest()
        {
            // Setup
            InMemInterestRelationIndex index = new InMemInterestRelationIndex();

            var routeElement1 = Guid.NewGuid();
            var routeElement2 = Guid.NewGuid();
            var routeElement3 = Guid.NewGuid();
            var routeElement4 = Guid.NewGuid();

            // Create walk of interest spanning route element 1,2,3
            var walkOfInterest1 = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { routeElement1, routeElement2, routeElement3 });
            index.Add(walkOfInterest1);

            // Create walk of interest spanning route element 2,3,4
            var walkOfInterest2 = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { routeElement2, routeElement3, routeElement4 });
            index.Add(walkOfInterest2);

            var routeElement1InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement1);
            var routeElement2InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement2);
            var routeElement3InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement3);
            var routeElement4InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement4);

            // Assert

            // Route element 1 has a relation to interest 1 only
            Assert.Single(routeElement1InterestRelations);
            Assert.Equal(walkOfInterest1.Id, routeElement1InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.Start, routeElement1InterestRelations[0].Item2);

            // Route element 2 has a relation to both interest 1 and 2
            Assert.Equal(2, routeElement2InterestRelations.Count);
            Assert.True(routeElement2InterestRelations.Exists(r => r.Item1 == walkOfInterest1.Id && r.Item2 == RouteNetworkInterestRelationKindEnum.PassThrough));
            Assert.True(routeElement2InterestRelations.Exists(r => r.Item1 == walkOfInterest2.Id && r.Item2 == RouteNetworkInterestRelationKindEnum.Start));

            // Route element 3 has a relation to both interest 1 and 2
            Assert.Equal(2, routeElement3InterestRelations.Count);
            Assert.True(routeElement3InterestRelations.Exists(r => r.Item1 == walkOfInterest1.Id && r.Item2 == RouteNetworkInterestRelationKindEnum.End));
            Assert.True(routeElement3InterestRelations.Exists(r => r.Item1 == walkOfInterest2.Id && r.Item2 == RouteNetworkInterestRelationKindEnum.PassThrough));

            // Route element 4 has a relation to interest 2 only
            Assert.Single(routeElement4InterestRelations);
            Assert.Equal(walkOfInterest2.Id, routeElement4InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.End, routeElement4InterestRelations[0].Item2);
        }

        [Fact]
        public void TestUpdatingIndex()
        {
            InMemInterestRelationIndex index = new InMemInterestRelationIndex();

            var routeElement1 = Guid.NewGuid();
            var routeElement2 = Guid.NewGuid();
            var routeElement3 = Guid.NewGuid();
            var routeElement4 = Guid.NewGuid();

            // Create a walk of interest spanning the first 3 route network elements
            var walkOfInterest = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { routeElement1, routeElement2, routeElement3 });
            index.Add(walkOfInterest);

            // Create a walk of interest spanning the last 3 route network elements
            var updatedWalkOfInterest = new RouteNetworkInterest(walkOfInterest.Id, RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { routeElement2, routeElement3, routeElement4 });
            index.Update(updatedWalkOfInterest, walkOfInterest);

            var routeElement1InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement1);
            var routeElement2InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement2);
            var routeElement3InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement3);
            var routeElement4InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement4);

            // Assert

            // Route element 1 has no relation to the interest
            Assert.Empty(routeElement1InterestRelations);

            // Route element 2 has a start relation to the interest
            Assert.Single(routeElement2InterestRelations);
            Assert.Equal(walkOfInterest.Id, routeElement2InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.Start, routeElement2InterestRelations[0].Item2);

            // Route element 3 has a passthroug relation to the interest
            Assert.Single(routeElement3InterestRelations);
            Assert.Equal(walkOfInterest.Id, routeElement3InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.PassThrough, routeElement3InterestRelations[0].Item2);

            // Route element 4 has an end relation to the interest
            Assert.Single(routeElement4InterestRelations);
            Assert.Equal(walkOfInterest.Id, routeElement4InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.End, routeElement4InterestRelations[0].Item2);
        }

        [Fact]
        public void TestRemoveWalkOfInterestFromIndex()
        {
            // Setup
            InMemInterestRelationIndex index = new InMemInterestRelationIndex();

            var routeElement1 = Guid.NewGuid();
            var routeElement2 = Guid.NewGuid();
            var routeElement3 = Guid.NewGuid();
            var routeElement4 = Guid.NewGuid();

            // Create walk of interest spanning route element 1,2,3
            var walkOfInterest1 = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { routeElement1, routeElement2, routeElement3 });
            index.Add(walkOfInterest1);

            // Create walk of interest spanning route element 2,3,4
            var walkOfInterest2 = new RouteNetworkInterest(Guid.NewGuid(), RouteNetworkInterestKindEnum.WalkOfInterest, new RouteNetworkElementIdList() { routeElement2, routeElement3, routeElement4 });
            index.Add(walkOfInterest2);

            // Remove the first interest
            index.Remove(walkOfInterest1);

            var routeElement1InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement1);
            var routeElement2InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement2);
            var routeElement3InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement3);
            var routeElement4InterestRelations = index.GetRouteNetworkElementInterestRelations(routeElement4);

            // Assert

            // Route element 1 has no relation to the interest
            Assert.Empty(routeElement1InterestRelations);

            // Route element 2 has a start relation to the interest
            Assert.Single(routeElement2InterestRelations);
            Assert.Equal(walkOfInterest2.Id, routeElement2InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.Start, routeElement2InterestRelations[0].Item2);

            // Route element 3 has a passthroug relation to the interest
            Assert.Single(routeElement3InterestRelations);
            Assert.Equal(walkOfInterest2.Id, routeElement3InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.PassThrough, routeElement3InterestRelations[0].Item2);

            // Route element 4 has an end relation to the interest
            Assert.Single(routeElement4InterestRelations);
            Assert.Equal(walkOfInterest2.Id, routeElement4InterestRelations[0].Item1);
            Assert.Equal(RouteNetworkInterestRelationKindEnum.End, routeElement4InterestRelations[0].Item2);
        }
    }
}
