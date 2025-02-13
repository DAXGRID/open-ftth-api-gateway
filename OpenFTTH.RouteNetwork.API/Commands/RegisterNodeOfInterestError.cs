using FluentResults;

namespace OpenFTTH.RouteNetwork.API.Commands
{
    public class RegisterNodeOfInterestError : Error
    {
        public RegisterNodeOfInterestErrorCodes Code { get; }
        public RegisterNodeOfInterestError(RegisterNodeOfInterestErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
