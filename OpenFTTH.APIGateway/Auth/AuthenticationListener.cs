using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Newtonsoft.Json.Linq;
using GraphQL.Server.Transports.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net.Http;

namespace OpenFTTH.APIGateway.Auth
{
    public class AuthenticationListener : IOperationMessageListener
    {
        public static readonly string PRINCIPAL_KEY = "User";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserContextBuilder _builder;

        public AuthenticationListener(IHttpContextAccessor contextAccessor, IUserContextBuilder builder)
        {
            _httpContextAccessor = contextAccessor;
            _builder = builder;
        }

        public ClaimsPrincipal ValidateCurrentToken(string token)
        {
            var myIssuer = "http://auth.openftth.local/auth/realms/openftth";

            var httpClient = new HttpClient();
            var t = new ConfigurationManager<OpenIdConnectConfiguration>($"{myIssuer}/.well-known/openid-configuration",
                                                                         new OpenIdConnectConfigurationRetriever(),
                                                                         new HttpDocumentRetriever(httpClient) { RequireHttps = false });

            var result = t.GetConfigurationAsync().Result;

            var tokenHandler = new JwtSecurityTokenHandler();
            var claimsPrincipal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = myIssuer,
                ValidAudience = "account",
                IssuerSigningKeys = result.SigningKeys

            }, out SecurityToken validatedToken);

            return claimsPrincipal;
        }

        public Task BeforeHandleAsync(MessageHandlingContext context)
        {
            if (context.Message.Type == MessageType.GQL_CONNECTION_INIT)
            {
                var payload = context.Message.Payload as JObject;

                if (payload != null && payload.ContainsKey("Authorization"))
                {
                    var token = payload.Value<string>("Authorization");

                    // Save the user to the http context
                    try
                    {
                        var user = ValidateCurrentToken(token);
                        _httpContextAccessor.HttpContext.User = user;
                    }
                    catch
                    {
                        return context.Terminate();
                    }
                }
            }

            // Always insert the http context user into the message handling context properties
            // Note: any IDisposable item inside the properties bag will be disposed after this message is handled!
            //  So do not insert such items here, but use something like 'context[PRINCIPAL_KEY] = [...]'
            context.Properties[PRINCIPAL_KEY] = _httpContextAccessor.HttpContext?.User;

            if (_httpContextAccessor.HttpContext?.User == null || !_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
            {
                return context.Terminate();
            }

            return Task.CompletedTask;
        }

        public Task HandleAsync(MessageHandlingContext context) => Task.CompletedTask;
        public Task AfterHandleAsync(MessageHandlingContext context) => Task.CompletedTask;
    }
}
