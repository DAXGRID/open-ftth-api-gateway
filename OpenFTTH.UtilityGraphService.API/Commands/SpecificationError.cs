using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class SpecificationError : Error
    {
        public SpecificationErrorCodes Code { get; }
        public SpecificationError(SpecificationErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
