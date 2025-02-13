using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class ConnectTerminalsAtRouteNodeError : Error
    {
        public ConnectTerminalsAtRouteNodeErrorCodes Code { get; }
        public ConnectTerminalsAtRouteNodeError(ConnectTerminalsAtRouteNodeErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
