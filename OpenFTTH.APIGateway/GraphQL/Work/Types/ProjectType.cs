using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Work.API.Model;

namespace OpenFTTH.APIGateway.GraphQL.Work.Types
{
    public class ProjectType : ObjectGraphType<Project>
    {
        public ProjectType(ILogger<ProjectType> logger)
        {
            Field(x => x.MRID, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Name of project");

            Field<ListGraphType<WorkTaskType>>(
                name: "workTasks",
                description: "All work tasks belonging to the project",
                resolve: context =>
                {
                    return context.Source.WorkTasks;
                }
            );
        }
    }


}
