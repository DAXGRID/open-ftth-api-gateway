using OpenFTTH.CQRS;

namespace OpenFTTH.Work.API.Queries
{
    public class ProjectsAndWorkTasksQuery : IQuery<ProjectsAndWorkTasksQueryResult>
    {
        public string RequestName => typeof(ProjectsAndWorkTasksQuery).Name;

        public ProjectsAndWorkTasksQuery()
        {
        }
    }
}
