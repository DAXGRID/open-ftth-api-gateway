using OpenFTTH.Results;

namespace OpenFTTH.APIGateway.GraphQL.Work
{
    public class WorkError : Error
    {
        public WorkErrorCodes Code { get; }
        public WorkError(WorkErrorCodes errorCode, string errorMsg) : base(errorCode.ToString() + ": " + errorMsg)
        {
            this.Code = errorCode;
            Metadata.Add("ErrorCode", errorCode.ToString());
        }
    }
}
