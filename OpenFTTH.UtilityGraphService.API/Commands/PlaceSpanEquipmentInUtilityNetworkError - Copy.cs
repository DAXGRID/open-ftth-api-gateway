using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class PlaceSpanEquipmentInUtilityNetworkError : Error
    {
        public PlaceSpanEquipmentInUtilityNetworkErrorCodes Code { get; }
        public PlaceSpanEquipmentInUtilityNetworkError(PlaceSpanEquipmentInUtilityNetworkErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
