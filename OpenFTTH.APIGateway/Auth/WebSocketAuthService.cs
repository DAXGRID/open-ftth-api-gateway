using GraphQL;
using GraphQL.Server.Transports.AspNetCore.WebSockets;
using GraphQL.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenFTTH.APIGateway.Settings;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OpenFTTH.APIGateway.Auth
{
    class WebSocketAuthService : IWebSocketAuthenticationService
    {
        private readonly IGraphQLSerializer _serializer;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        private readonly AuthSetting _authSetting;
        private readonly ILogger<WebSocketAuthService> _logger;

        public WebSocketAuthService(
            IGraphQLSerializer serializer,
            IOptions<AuthSetting> authSetting,
            HttpClient httpClient,
            ILogger<WebSocketAuthService> logger)
        {
            _serializer = serializer;
            _authSetting = authSetting.Value;
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_authSetting.Host}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(httpClient) { RequireHttps = _authSetting.RequireHttps });
            _logger = logger;
        }

        public async Task AuthenticateAsync(IWebSocketConnection connection, string subProtocol, OperationMessage operationMessage)
        {
            try
            {
                var payload = _serializer.ReadNode<Inputs>(operationMessage.Payload);
                if ((payload?.TryGetValue("Authorization", out var value) ?? false) && value is string valueString)
                {
                    // We remove the `Bearer` part since it is not part of the token.
                    var token = valueString.ToString().Replace("Bearer ", string.Empty);
                    var user = await ParseToken(token);
                    if (user is not null)
                    {
                        // set user indicates authentication was successful
                        connection.HttpContext.User = user;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // In case the operation is cancelled before it is finished, we do nothing.
                // The `HttpContext.User` won't be set so the user won't be authorized.
            }
            catch (SecurityTokenExpiredException ex)
            {
                // In case the security token is expired, we do nothing.
                // The `HttpContext.User` won't be set so the user won't be authorized.
                _logger.LogDebug("ex", ex.Message);
            }
            catch (ArgumentException ex)
            {
                // This can happen if no token was sent with the request.
                // This is not a critical error, we just do not log them in.
                _logger.LogDebug("ex", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("{Exception}", ex.Message);
            }
        }

        private async Task<ClaimsPrincipal> ParseToken(string authorizationHeaderValue)
        {
            var result = await _configurationManager.GetConfigurationAsync();

            return new JwtSecurityTokenHandler().ValidateToken(authorizationHeaderValue, new TokenValidationParameters
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
    }
}
