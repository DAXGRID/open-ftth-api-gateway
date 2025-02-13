using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class RemoveNodeContainerFromRouteNetworkError : Error
    {
        public RemoveNodeContainerFromRouteNetworkErrorCodes Code { get; }
        public RemoveNodeContainerFromRouteNetworkError(RemoveNodeContainerFromRouteNetworkErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
