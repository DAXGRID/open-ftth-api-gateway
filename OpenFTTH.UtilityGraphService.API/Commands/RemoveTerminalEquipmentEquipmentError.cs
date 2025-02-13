using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class RemoveTerminalEquipmentError : Error
    {
        public RemoveTerminalEquipmentErrorCodes Code { get; }
        public RemoveTerminalEquipmentError(RemoveTerminalEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
