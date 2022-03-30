using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.Work.API.Model;

namespace OpenFTTH.APIGateway.GraphQL.Work.Types
{
    public class UserWorkContextType : ObjectGraphType<UserWorkContext>
    {
        public UserWorkContextType(ILogger<UserWorkContextType> logger)
        {
            Field(x => x.UserName, type: typeof(StringGraphType)).Description("User name/id");
            Field(x => x.CurrentWorkTask, type: typeof(WorkTaskType)).Description("CurrentWorkTask");
        }
    }
}
