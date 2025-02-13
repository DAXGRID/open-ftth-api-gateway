using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class CutSpanSegmentsAtRouteNodeError : Error
    {
        public CutSpanSegmentsAtRouteNodeErrorCodes Code { get; }
        public CutSpanSegmentsAtRouteNodeError(CutSpanSegmentsAtRouteNodeErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
