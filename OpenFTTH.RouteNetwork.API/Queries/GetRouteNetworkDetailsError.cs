using FluentResults;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    public class GetRouteNetworkDetailsError : Error
    {
        public GetRouteNetworkDetailsErrorCodes Code { get; }
        public GetRouteNetworkDetailsError(GetRouteNetworkDetailsErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
