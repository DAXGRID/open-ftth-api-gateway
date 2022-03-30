using OpenFTTH.Work.API.Queries;
using OpenFTTH.Work.Business.InMemTestImpl;
using Xunit;

namespace OpenFTTH.WorkService.Tests
{
    public class QueryTests
    {
        [Fact]
        public void TestProjectsAndWorkTasksQuery()
        {
            var repo = new InMemRepoImpl();

            var queryHandler = new ProjectsAndWorkTasksQueryHandler(repo);

            var queryResult = queryHandler.HandleAsync(new ProjectsAndWorkTasksQuery()).Result;

            // Check that minimum 2 projects are returned
            Assert.True(queryResult.Projects.Count > 1);

            // Check that the two first projects has minumum 2 work tasks
            Assert.True(queryResult.Projects[0].WorkTasks.Count > 1);
            Assert.True(queryResult.Projects[1].WorkTasks.Count > 0);
        }
    }
}
