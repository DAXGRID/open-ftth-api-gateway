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
using System.Linq;

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
            if (MessageType.GQL_CONNECTION_INIT.Equals(context.Message?.Type))
            {
                var payload = context.Message?.Payload;
                if (payload != null)
                {
                    var authorizationTokenObject = ((JObject)payload)["Authorization"];

                    if (authorizationTokenObject != null)
                    {
                        var token = authorizationTokenObject.ToString().Replace("Bearer ", string.Empty);
                        _httpContextAccessor.HttpContext.User = ValidateCurrentToken(token);
                    }
                }
            }

            context.Properties["GraphQLUserContext"] = new GraphQLUserContext() { User = _httpContextAccessor.HttpContext.User };

            return Task.CompletedTask;
        }

        public Task HandleAsync(MessageHandlingContext context) => Task.CompletedTask;
        public Task AfterHandleAsync(MessageHandlingContext context) => Task.CompletedTask;
    }
}
