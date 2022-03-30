using OpenFTTH.CQRS;
using OpenFTTH.Work.API.Model;

namespace OpenFTTH.Work.API.Queries
{
    public class UserWorkContextQueryResult : IQuery<UserWorkContextQueryResult>
    {
        public UserWorkContextQueryResult(UserWorkContext userWorkContext) => UserWorkContext = userWorkContext;

        public UserWorkContext UserWorkContext { get; }
    }
}
