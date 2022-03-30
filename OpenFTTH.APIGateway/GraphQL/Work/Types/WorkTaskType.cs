using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.Work.API.Model;

namespace OpenFTTH.APIGateway.GraphQL.Work.Types
{
    public class WorkTaskType : ObjectGraphType<WorkTask>
    {
        public WorkTaskType(ILogger<WorkTaskType> logger)
        {
            Field(x => x.MRID, type: typeof(IdGraphType)).Description("Master Resource Identifier UUID Property");
            Field(x => x.Name, type: typeof(StringGraphType)).Description("Name/descrription of work task");
            Field(x => x.WorkTaskType, type: typeof(StringGraphType)).Description("The type/category of the worktask - from work mgmt. system");
            Field(x => x.Status, type: typeof(StringGraphType)).Description("The status of the worktask - from work mgmt. system");
            Field(x => x.AddressString, type: typeof(StringGraphType)).Description("Address description");
            Field(x => x.CentralOfficeArea, type: typeof(StringGraphType)).Description("Central office area");
            Field(x => x.FlexPointArea, type: typeof(StringGraphType)).Description("Flex point area");
            Field(x => x.SplicePointArea, type: typeof(StringGraphType)).Description("Splice point area");
            Field(x => x.InstallationId, type: typeof(StringGraphType)).Description("Installation id");
            Field(x => x.Technology, type: typeof(StringGraphType)).Description("Technology (PON, PtP etc)");
            Field(x => x.Project, type: typeof(ProjectType)).Description("Project");

            Field<GeometryType>(
               name: "geometry",
               description: "The location of the work task (geojson)",
               resolve: context =>
               {
                   return Geometry.MapFromNTSGeometry(context.Source.Location);
               }
           );
        }
    }


}
