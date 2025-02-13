using FluentAssertions;
using FluentResults;
using OpenFTTH.CQRS;
using OpenFTTH.RouteNetwork.API.Commands;
using OpenFTTH.RouteNetwork.API.Model;
using OpenFTTH.RouteNetwork.Tests.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;

namespace OpenFTTH.RouteNetwork.Tests
{
    public class InterestValidationTests : IClassFixture<TestRouteNetwork>
    {
        private ICommandDispatcher _commandDispatcher;
        private IQueryDispatcher _queryDispatcher;

        public InterestValidationTests(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            _commandDispatcher = commandDispatcher;
            _queryDispatcher = queryDispatcher;
        }

        [Fact]
        public async Task ValidateValidWalk_ShouldSucceed()
        {
            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S2, TestRouteNetwork.S1, TestRouteNetwork.S4 };
            var validateInterestCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walk);

            var validateResult = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand);

            // Assert
            validateResult.IsSuccess.Should().BeTrue();

            validateResult.Value.NodeIds.Count.Should().Be(4);
            validateResult.Value.SegmentIds.Count.Should().Be(3);

            validateResult.Value.NodeIds[0].Should().Be(TestRouteNetwork.CO_1);
            validateResult.Value.NodeIds[1].Should().Be(TestRouteNetwork.HH_1);
            validateResult.Value.NodeIds[2].Should().Be(TestRouteNetwork.HH_2);
            validateResult.Value.NodeIds[3].Should().Be(TestRouteNetwork.CC_1);

            validateResult.Value.SegmentIds[0].Should().Be(TestRouteNetwork.S1);
            validateResult.Value.SegmentIds[1].Should().Be(TestRouteNetwork.S2);
            validateResult.Value.SegmentIds[2].Should().Be(TestRouteNetwork.S4);
        }

        [Fact]
        public async Task ValidateValidWalkWithBothNodeAndSegmentIds_ShouldSucceed()
        {
            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.CO_1, TestRouteNetwork.S1, TestRouteNetwork.HH_1, TestRouteNetwork.S2, TestRouteNetwork.HH_2, TestRouteNetwork.S4, TestRouteNetwork.CC_1 };
            var validateInterestCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walk);

            var validateResult = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand);

            // Assert
            validateResult.IsSuccess.Should().BeTrue();

            validateResult.Value.NodeIds.Count.Should().Be(4);
            validateResult.Value.SegmentIds.Count.Should().Be(3);

            validateResult.Value.NodeIds[0].Should().Be(TestRouteNetwork.CO_1);
            validateResult.Value.NodeIds[1].Should().Be(TestRouteNetwork.HH_1);
            validateResult.Value.NodeIds[2].Should().Be(TestRouteNetwork.HH_2);
            validateResult.Value.NodeIds[3].Should().Be(TestRouteNetwork.CC_1);

            validateResult.Value.SegmentIds[0].Should().Be(TestRouteNetwork.S1);
            validateResult.Value.SegmentIds[1].Should().Be(TestRouteNetwork.S2);
            validateResult.Value.SegmentIds[2].Should().Be(TestRouteNetwork.S4);
        }

        [Fact]
        public async Task ValidateValidWalkWithBothNodeAndSegmentIdsThatOverlap_ShouldSucceed()
        {
            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.HH_1, TestRouteNetwork.S1, TestRouteNetwork.CO_1, TestRouteNetwork.S1, TestRouteNetwork.HH_1 };
            var validateInterestCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walk);

            var validateResult = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand);

            // Assert
            validateResult.IsSuccess.Should().BeTrue();

            validateResult.Value.NodeIds.Count.Should().Be(3);
            validateResult.Value.SegmentIds.Count.Should().Be(2);
        }

        [Fact]
        public async Task ValidateInvalidWalkWithBothNodeAndSegmentIds_ShouldFail()
        {
            // There's a hole in this walk
            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.HH_1, TestRouteNetwork.S1, TestRouteNetwork.CO_1, TestRouteNetwork.S1, TestRouteNetwork.HH_1, TestRouteNetwork.S4, TestRouteNetwork.CC_1 };
            var validateInterestCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walk);

            var validateResult = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand);

            // Assert
            validateResult.IsFailed.Should().BeTrue();
        }



        [Fact]
        public async Task ValidateInvalidWalk_ShouldFail()
        {
            // There's a hole in this walk
            var walk = new RouteNetworkElementIdList() { TestRouteNetwork.S2, TestRouteNetwork.S1, TestRouteNetwork.S5 };
            var validateInterestCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), walk);

            var validateResult = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand);

            // Assert
            validateResult.IsFailed.Should().BeTrue();
        }

        [Fact]
        public async Task ValidatedWalkEqualTest_ShouldSucceed()
        {
            // We create two walks. One that goes CO_1 -> HH_1 -> HH_2 -> to CC_1, and one that goes the opposite way.
            // These two walks are to be treated as equal

            var validateInterestForwardCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), new RouteNetworkElementIdList() { TestRouteNetwork.S2, TestRouteNetwork.S1, TestRouteNetwork.S4 });
            var validateResultForward = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestForwardCommand);

            var validateInterestReversedCommand = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), new RouteNetworkElementIdList() { TestRouteNetwork.S4, TestRouteNetwork.S2, TestRouteNetwork.S1 });
            var validateResultReversed = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestReversedCommand);


            // Assert
            validateResultForward.IsSuccess.Should().BeTrue();
            validateResultReversed.IsSuccess.Should().BeTrue();
            validateResultForward.Value.FromNodeId.Should().Be(TestRouteNetwork.CO_1);
            validateResultReversed.Value.FromNodeId.Should().Be(TestRouteNetwork.CC_1);

            var walk1 = new ValidatedRouteNetworkWalk(validateResultForward.Value.RouteNetworkElementRefs);
            var walk2 = new ValidatedRouteNetworkWalk(validateResultReversed.Value.RouteNetworkElementRefs);

            walk1.Equals(walk2).Should().BeTrue();
        }

        [Fact]
        public async Task ValidatedWalkEqualTest_ShouldFail()
        {
            // We create two walks that are not equal
            var validateInterestCommand1 = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), new RouteNetworkElementIdList() { TestRouteNetwork.S1, TestRouteNetwork.S2 });
            var validateResult1 = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand1);

            var validateInterestCommand2 = new ValidateWalkOfInterest(Guid.NewGuid(), new UserContext("test", Guid.Empty), new RouteNetworkElementIdList() { TestRouteNetwork.S1 });
            var validateResult2 = await _commandDispatcher.HandleAsync<ValidateWalkOfInterest, Result<ValidatedRouteNetworkWalk>>(validateInterestCommand2);


            // Assert
            validateResult1.IsSuccess.Should().BeTrue();
            validateResult2.IsSuccess.Should().BeTrue();

            var walk1 = new ValidatedRouteNetworkWalk(validateResult1.Value.RouteNetworkElementRefs);
            var walk2 = new ValidatedRouteNetworkWalk(validateResult2.Value.RouteNetworkElementRefs);

            walk1.Equals(walk2).Should().BeFalse();
        }
    }
}
