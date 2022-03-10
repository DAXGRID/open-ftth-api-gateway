using OpenFTTH.CQRS;

namespace OpenFTTH.Work.API.Queries
{
    public class UserWorkContextQuery : IQuery<UserWorkContextQueryResult>
    {
        public string RequestName => typeof(UserWorkContextQuery).Name;

        public string UserName { get; }

        public UserWorkContextQuery(string userName)
        {
            UserName = userName;
        }
    }
}
