using GraphQL;
using GraphQL.Types;
using OpenFTTH.APIGateway.GraphQL.Work.Types;
using OpenFTTH.CQRS;
using OpenFTTH.Work.API.Mutations;
using System;

namespace OpenFTTH.APIGateway.GraphQL.Work.Mutations
{
    public class UserWorkContextMutations : ObjectGraphType
    {
        private readonly ICommandDispatcher _commandDispatcher;

        public UserWorkContextMutations(ICommandDispatcher commandDispatcher)
        {
            _commandDispatcher = commandDispatcher;

            Description = "User context information mutations";

            Field<UserWorkContextType>(
              "setCurrentWorkTask",
              description: "Mutation used set work task id on given user",
              arguments: new QueryArguments(
                  new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "userName" },
                  new QueryArgument<NonNullGraphType<IdGraphType>> { Name = "workTaskId" }
              ),
              resolve: context =>
              {
                  var userName = context.GetArgument<string>("userName");
                  var workTaskId = context.GetArgument<Guid>("workTaskId");

                  var mutationResult = this._commandDispatcher.HandleAsync<SetUserCurrentWorkTaskMutation, SetUserCurrentWorkTaskMutationResult>(new SetUserCurrentWorkTaskMutation(userName, workTaskId)).Result;

                  return mutationResult.UserWorkContext;
              }
            );
        }
    }
}
