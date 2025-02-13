using FluentResults;

namespace OpenFTTH.RouteNetwork.API.Queries
{
    public class FindNearestRouteNodesError : Error
    {
        public FindNearestRouteNodesErrorCodes Code { get; }
        public FindNearestRouteNodesError(FindNearestRouteNodesErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
