using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class DisconnectSpanSegmentsAtRouteNodeError : Error
    {
        public DisconnectSpanSegmentsAtRouteNodeErrorCodes Code { get; }
        public DisconnectSpanSegmentsAtRouteNodeError(DisconnectSpanSegmentsAtRouteNodeErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
