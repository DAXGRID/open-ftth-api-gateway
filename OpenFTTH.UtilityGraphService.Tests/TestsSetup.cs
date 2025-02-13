using OpenFTTH.EventSourcing;
using Xunit;
using Xunit.Extensions.Ordering;

// This class is made as a place to run before all other ordered tests.
namespace OpenFTTH.UtilityGraphService.Tests
{
    [Order(0)]
    public class TestsSetup
    {
        public TestsSetup(IEventStore eventStore)
        {
            eventStore.ScanForProjections();
        }

        // This dummy test is made to make sure that the constructor is called.
        // This is a hack, but the easiest way to
        // make sure that all projections are scanned.
        [Fact]
        public void Dummy()
        {
            Assert.True(true);
        }
    }
}
