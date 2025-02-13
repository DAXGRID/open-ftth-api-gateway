using FluentResults;

namespace OpenFTTH.RouteNetwork.API.Commands
{
    public class RegisterWalkOfInterestError : Error
    {
        public RegisterWalkOfInterestErrorCodes Code { get; }
        public RegisterWalkOfInterestError(RegisterWalkOfInterestErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
