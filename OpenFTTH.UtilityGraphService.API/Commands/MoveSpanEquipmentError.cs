using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class MoveSpanEquipmentError : Error
    {
        public MoveSpanEquipmentErrorCodes Code { get; }
        public MoveSpanEquipmentError(MoveSpanEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
