using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace OpenFTTH.APIGateway
{
    public class GraphQLUserContext : Dictionary<string, object>
    {
        public ClaimsPrincipal User { get; set; }

        public GraphQLUserContext(HttpContext context)
        {
            User = context.User;
        }

        public string Username => User?.Claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value;
    }
}
