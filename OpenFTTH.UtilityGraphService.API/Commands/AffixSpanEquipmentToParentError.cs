using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class AffixSpanEquipmentToParentError : Error
    {
        public AffixSpanEquipmentToParentErrorCodes Code { get; }
      
        public AffixSpanEquipmentToParentError(AffixSpanEquipmentToParentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
