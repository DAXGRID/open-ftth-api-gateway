namespace OpenFTTH.Work.API.Model
{
    public class UserWorkContext
    {
        public UserWorkContext(string userName, WorkTask? currentWorkTask)
        {
            UserName = userName;
            CurrentWorkTask = currentWorkTask;
        }

        public string UserName { get; set; }

        public WorkTask? CurrentWorkTask { get; set; }
    }
}
