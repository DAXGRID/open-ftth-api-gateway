using OpenFTTH.Results;
using OpenFTTH.CQRS;
using OpenFTTH.Work.API.Model;
using OpenFTTH.Work.API.Queries;
using System;
using System.Linq;

namespace OpenFTTH.APIGateway.GraphQL.Work
{
    public static class WorkQueryHelper
    {
        public static Result<Guid> GetUserCurrentWorkId(string userName, IQueryDispatcher queryDispatcher)
        {
            // If debug accept that no work task is selected
#if DEBUG
            if (userName == null)
                return Result.Ok(Guid.Empty);
#endif

            var queryRequest = new GetUserWorkContext(userName);

            var queryResult = queryDispatcher.HandleAsync<GetUserWorkContext, Result<UserWorkContext>>(queryRequest).Result;

            if (queryResult.IsFailed)
                return Result.Fail(queryResult.Errors.First());

            if (queryResult.Value.CurrentWorkTask == null)
                return Result.Fail(new WorkError(WorkErrorCodes.USER_CURRENT_WORK_TASK_NOT_SET, $"The user: '{userName}' has not been assigned to any work task. Can therefore not do any edits."));

            return Result.Ok(queryResult.Value.CurrentWorkTask.Id);
        }
    }
}
