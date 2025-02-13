using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class UpdateEquipmentPropertiesError : Error
    {
        public UpdateEquipmentPropertiesErrorCodes Code { get; }
        public UpdateEquipmentPropertiesError(UpdateEquipmentPropertiesErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
