using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.CQRS;
using OpenFTTH.TestData;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class TestDataMutations : ObjectGraphType
    {
        public TestDataMutations(ILoggerFactory loggerFactory, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
        {
            Description = "Test network mutations.";

            Field<CommandResultType>(
              "createSpecifications",
              description: "Create a set of specifications for testing",
              resolve: context =>
              {
                  var result = new TestSpecifications(loggerFactory, commandDispatcher, queryDispatcher).Run();

                  return new CommandResult(result);
              }
            );
        }
    }
}
