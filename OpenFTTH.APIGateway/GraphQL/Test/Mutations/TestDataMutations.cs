using GraphQL.Types;
using Microsoft.Extensions.Logging;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Core.Model;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.TestData;

namespace OpenFTTH.APIGateway.GraphQL.RouteNetwork.Mutations
{
    public class TestDataMutations : ObjectGraphType
    {
        public TestDataMutations(ILoggerFactory loggerFactory, ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher, IEventStore eventStore)
        {
            Description = "Test network mutations.";

            Field<CommandResultType>("createSpecifications")
              .Description("Create a set of specifications for testing")
              .Resolve(context =>
              {
                  var result = new TestSpecifications(loggerFactory, commandDispatcher, queryDispatcher, eventStore).Run();

                  return new CommandResult(result);
              });
        }
    }
}
