using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class TerminalEquipmentError : Error
    {
        public TerminalEquipmentErrorCodes Code { get; }
        public TerminalEquipmentError(TerminalEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
