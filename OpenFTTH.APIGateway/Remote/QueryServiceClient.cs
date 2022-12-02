using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Remote
{
    public class QueryServiceClient<QueryServiceType>
    {
        private readonly ILogger<QueryServiceClient<QueryServiceType>> _logger;
        private readonly string _serviceUrl;

        public QueryServiceClient(ILogger<QueryServiceClient<QueryServiceType>> logger, string serviceUrl)
        {
            _logger = logger;
            _serviceUrl = serviceUrl;
        }

        public async Task<ResponseType> Query<RequestType, ResponseType>(RequestType queryCommand)
        {
            var clientHandler = new HttpClientHandler();
            // This is an ugly hack, the reason that this is needed is because of certicates generated does not work on Linux
            // It won't provide security risk as long as you use a Service Mesh for TLS - if you don't use a SM, then find a solution.
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

            using (var httpClient = new HttpClient(clientHandler))
            {
                string requestUrl = _serviceUrl + "/query";

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(requestUrl),
                    Content = new StringContent(JsonConvert.SerializeObject(queryCommand), Encoding.UTF8, "application/json")
                };

                _logger.LogDebug($"Sending query request: {queryCommand.GetType().Name} to service: {requestUrl}");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

                return (ResponseType)JsonConvert.DeserializeObject(responseBody, typeof(ResponseType));
            }
        }
    }
}
