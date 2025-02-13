using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class ConnectSpanEquipmentAndTerminalEquipmentError : Error
    {
        public ConnectSpanEquipmentAndTerminalEquipmentErrorCodes Code { get; }
        public ConnectSpanEquipmentAndTerminalEquipmentError(ConnectSpanEquipmentAndTerminalEquipmentErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
