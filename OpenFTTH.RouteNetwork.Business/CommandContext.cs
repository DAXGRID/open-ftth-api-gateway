using OpenFTTH.CQRS;
using System;

namespace OpenFTTH.RouteNetwork.Business
{
    public record CommandContext
    {
        public Guid CorrelationId { get; }
        public Guid CmdId { get;}
        public UserContext UserContext { get; }

        public CommandContext(Guid correlationId, Guid cmdId, UserContext userContext)
        {
            CorrelationId = correlationId;
            CmdId = cmdId;
            UserContext = userContext;
        }
    }
}
