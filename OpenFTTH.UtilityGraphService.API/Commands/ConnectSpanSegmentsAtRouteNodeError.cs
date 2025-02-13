using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class ConnectSpanSegmentsAtRouteNodeError : Error
    {
        public ConnectSpanSegmentsAtRouteNodeErrorCodes Code { get; }
        public ConnectSpanSegmentsAtRouteNodeError(ConnectSpanSegmentsAtRouteNodeErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
