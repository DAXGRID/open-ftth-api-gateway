using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class PlaceAdditionalStructuresInSpanEquipmentError : Error
    {
        public PlaceAdditionalStructuresInSpanEquipmentErrorCodes Code { get; }
        public PlaceAdditionalStructuresInSpanEquipmentError(PlaceAdditionalStructuresInSpanEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
