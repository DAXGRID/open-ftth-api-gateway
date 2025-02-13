using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class PlaceSpanEquipmentInRouteNetworkError : Error
    {
        public PlaceSpanEquipmentInRouteNetworkErrorCodes Code { get; }
        public PlaceSpanEquipmentInRouteNetworkError(PlaceSpanEquipmentInRouteNetworkErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
