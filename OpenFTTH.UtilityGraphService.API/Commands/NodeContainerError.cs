using FluentResults;

namespace OpenFTTH.UtilityGraphService.API.Commands
{
    public class NodeContainerError : Error
    {
        public NodeContainerErrorCodes Code { get; }

        public NodeContainerError(NodeContainerErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
