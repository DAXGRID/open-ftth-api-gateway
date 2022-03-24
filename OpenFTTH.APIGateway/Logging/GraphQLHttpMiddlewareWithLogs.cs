using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Logging
{
    public class GraphQLHttpMiddlewareWithLogs<TSchema> : GraphQLHttpMiddleware<TSchema>
        where TSchema : ISchema
    {
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GraphQLHttpMiddlewareWithLogs(
            ILogger<GraphQLHttpMiddleware<TSchema>> logger,
            RequestDelegate next,
            IHttpContextAccessor httpContextAccessor,
            IGraphQLRequestDeserializer requestDeserializer)
            : base(next, requestDeserializer)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task RequestExecutedAsync(in GraphQLRequestExecutionResult requestExecutionResult)
        {
            if (requestExecutionResult.Result.Errors != null)
            {
                var username = _httpContextAccessor.HttpContext.User?.Claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value ?? "";
                var failedQuery = GetQueryWithParameters(requestExecutionResult);
                if (requestExecutionResult.IndexInBatch.HasValue)
                    _logger.LogError(@$"User: {username} - GraphQL execution with error(s) in batch [{requestExecutionResult.IndexInBatch}]: {requestExecutionResult.Result.Errors}\n{failedQuery}");
                else
                    _logger.LogError($"User: {username} - GraphQL execution with error(s): {requestExecutionResult.Result.Errors}.\n{failedQuery}");
            }
            else
                _logger.LogDebug("GraphQL execution successfully completed in {Elapsed}", requestExecutionResult.Elapsed);

            return base.RequestExecutedAsync(requestExecutionResult);
        }

        protected override CancellationToken GetCancellationToken(HttpContext context)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                base.GetCancellationToken(context), new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);
            return cts.Token;
        }

        private static string GetQueryWithParameters(GraphQLRequestExecutionResult requestExecutionResult)
        {
            var failedQuery = string.Empty;
            if (requestExecutionResult.Request?.Inputs is not null)
            {
                var inputs = JsonConvert.SerializeObject(requestExecutionResult.Request.Inputs);
                failedQuery = $"Inputs: {inputs}\nQuery/Mutation: {requestExecutionResult.Request.Query}";
            }
            else
            {
                failedQuery = $"Inputs: None\nQuery/Mutation: {requestExecutionResult.Request.Query}";
            }

            return failedQuery;
        }
    }
}
