using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class DetachSpanEquipmentFromNodeContainerError : Error
    {
        public DetachSpanEquipmentFromNodeContainerErrorCodes Code { get; }
        public DetachSpanEquipmentFromNodeContainerError(DetachSpanEquipmentFromNodeContainerErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
