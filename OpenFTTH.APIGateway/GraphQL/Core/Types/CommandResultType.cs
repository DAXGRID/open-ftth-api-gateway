using GraphQL.DataLoader;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Core.Model;

namespace OpenFTTH.APIGateway.CoreTypes
{
    public class CommandResultType : ObjectGraphType<CommandResult>
    {
        public CommandResultType(IDataLoaderContextAccessor dataLoader)
        {
            Description = "Error code and message";

            Field("IsSuccess", x => x.IsSuccess, type: typeof(BooleanGraphType)).Description("True if the command was successfully processed on the server side.");
            Field("ErrorCode", x => x.ErrorCode, type: typeof(StringGraphType)).Description("Error code. Please see error code lists defined in the underlying services called");
            Field("ErrorMessage", x => x.ErrorMessage, type: typeof(StringGraphType)).Description("Error message");
        }
    }
}
