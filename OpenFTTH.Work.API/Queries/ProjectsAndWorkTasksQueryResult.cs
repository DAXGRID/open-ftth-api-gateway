using OpenFTTH.Work.API.Model;
using System.Collections.Generic;

namespace OpenFTTH.Work.API.Queries
{
    public class ProjectsAndWorkTasksQueryResult
    {
        public ProjectsAndWorkTasksQueryResult(List<Project> projects) => Projects = projects;

        public List<Project> Projects { get; }
    }
}
