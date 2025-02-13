using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class RemoveSpanStructureFromSpanEquipmentError : Error
    {
        public RemoveSpanStructureFromSpanEquipmentErrorCodes Code { get; }
        public RemoveSpanStructureFromSpanEquipmentError(RemoveSpanStructureFromSpanEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
