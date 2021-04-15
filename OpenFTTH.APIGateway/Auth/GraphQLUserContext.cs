using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using GraphQL.Authorization;

namespace OpenFTTH.APIGateway
{
    /// <summary>
    /// Custom context class that implements <see cref="IProvideClaimsPrincipal"/>.
    /// </summary>
    public class GraphQLUserContext : Dictionary<string, object>, IProvideClaimsPrincipal
    {
        /// <inheritdoc />
        public ClaimsPrincipal User { get; set; }

        public string Username => User?.Claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value;
    }
}
