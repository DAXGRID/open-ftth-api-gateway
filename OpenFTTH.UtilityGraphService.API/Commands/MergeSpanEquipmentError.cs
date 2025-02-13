using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class MergeSpanEquipmentError : Error
    {
        public MergeSpanEquipmentErrorCodes Code { get; }
        public MergeSpanEquipmentError(MergeSpanEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
