using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class AffixSpanEquipmentToNodeContainerError : Error
    {
        public AffixSpanEquipmentToNodeContainerErrorCodes Code { get; }
        public AffixSpanEquipmentToNodeContainerError(AffixSpanEquipmentToNodeContainerErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
