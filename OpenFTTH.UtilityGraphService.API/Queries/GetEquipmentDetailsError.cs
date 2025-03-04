using OpenFTTH.Results;

namespace OpenFTTH.UtilityGraphService.API.Queries
{
    public class GetEquipmentDetailsError : Error
    {
        public GetEquipmentDetailsErrorCodes Code { get; }
        public GetEquipmentDetailsError(GetEquipmentDetailsErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
