using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class UpdateNodeContainerPropertiesError : Error
    {
        public UpdateNodeContainerPropertiesErrorCodes Code { get; }
        public UpdateNodeContainerPropertiesError(UpdateNodeContainerPropertiesErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
