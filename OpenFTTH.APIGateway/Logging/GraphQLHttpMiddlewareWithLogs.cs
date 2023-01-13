using GraphQL;
using GraphQL.Server.Transports.AspNetCore;
using GraphQL.Transport;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Logging
{
    public class GraphQLHttpMiddlewareWithLogs<TSchema> : GraphQLHttpMiddleware<TSchema>
        where TSchema : ISchema
    {
        private readonly ILogger _logger;

        public GraphQLHttpMiddlewareWithLogs(
            RequestDelegate next,
            IGraphQLTextSerializer serializer,
            IDocumentExecuter<TSchema> documentExecuter,
            IServiceScopeFactory serviceScopeFactory,
            GraphQLHttpMiddlewareOptions options,
            IHostApplicationLifetime hostApplicationLifetime,
            ILogger<GraphQLHttpMiddleware<TSchema>> logger)
            : base(next, serializer, documentExecuter, serviceScopeFactory, options, hostApplicationLifetime)
        {
            _logger = logger;
        }

        protected override async Task<ExecutionResult> ExecuteRequestAsync(
            HttpContext context,
            GraphQLRequest request,
            IServiceProvider serviceProvider,
            IDictionary<string, object> userContext)
        {
            var executionResult = await base.ExecuteRequestAsync(context, request, serviceProvider, userContext);
            if (executionResult.Errors is not null)
            {
                var username = context.User?.Claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value ?? "USERNAME NOT FOUND";
                var failedQuery = GetQueryWithParameters(request);
                _logger.LogError(
                    "User: {Username} - GraphQL execution with error(s): {Errors}.\n{FailedQuery}",
                    username,
                    executionResult.Errors,
                    failedQuery);
            }

            return executionResult;
        }

        private static string GetQueryWithParameters(GraphQLRequest request)
        {
            var failedQuery = string.Empty;
            if (request.Variables is not null)
            {
                var inputs = JsonConvert.SerializeObject(request.Variables);
                failedQuery = $"Inputs: {inputs}\nQuery/Mutation: {request.Query}";
            }
            else
            {
                failedQuery = $"Inputs: None\nQuery/Mutation: {request.Query}";
            }

            return failedQuery;
        }
    }
}
