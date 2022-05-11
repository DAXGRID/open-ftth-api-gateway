using GraphQL.Server.Transports.Subscriptions.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using OpenFTTH.APIGateway.Settings;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Auth
{
    public class AuthenticationListener : IOperationMessageListener
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AuthSetting _authSetting;
        private readonly HttpClient _httpClient;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

        public AuthenticationListener(IHttpContextAccessor contextAccessor,
                                      IOptions<AuthSetting> authSetting,
                                      HttpClient httpClient)
        {
            _httpContextAccessor = contextAccessor;
            _authSetting = authSetting.Value;
            _httpClient = httpClient;
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_authSetting.Host}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(_httpClient) { RequireHttps = _authSetting.RequireHttps });
        }

        public async Task<ClaimsPrincipal> RetrieveIdentityPrincipal(string token)
        {
            var result = await _configurationManager.GetConfigurationAsync();

            var tokenHandler = new JwtSecurityTokenHandler();
            return tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = _authSetting.Audience,
                ValidateIssuer = true,
                ValidIssuer = _authSetting.Host,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = result.SigningKeys,
                RequireExpirationTime = true,
                ValidateLifetime = true,
                RequireSignedTokens = true
            }, out SecurityToken validatedToken);
        }

        public async Task BeforeHandleAsync(MessageHandlingContext context)
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
                        _httpContextAccessor.HttpContext.User = await RetrieveIdentityPrincipal(token);
                    }
                }
            }

            context.Properties["GraphQLUserContext"] = new GraphQLUserContext()
            {
                User = _httpContextAccessor.HttpContext.User
            };
        }

        public Task HandleAsync(MessageHandlingContext context) => Task.CompletedTask;
        public Task AfterHandleAsync(MessageHandlingContext context) => Task.CompletedTask;
    }
}
