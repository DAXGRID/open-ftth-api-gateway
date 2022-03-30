namespace OpenFTTH.Work.Business.InMemTestImpl
{
    /*
    /// <summary>
    /// Simple in-memory implementation of the WorkService API to be used for testing and demo purpose
    /// </summary>
    public class InMemWorkServiceImpl : IWorkServiceAPI
    {
        private readonly InMemRepoImpl _data = new InMemRepoImpl();


        public IQueryResult Query(IQueryCommand queryCommand)
        {
            switch (queryCommand)
            {
                case ProjectsAndWorkTasksQuery query:
                    return (IQueryResult)Query(query);
                case UserWorkContextQuery query:
                    return (IQueryResult)Query(query);
            }

            throw new ArgumentException("No implementation found for query command: " + queryCommand.GetType().FullName);
        }


        public IMutationResult Mutate(ICommand mutationCommand)
        {
            switch (mutationCommand)
            {
                case SetUserCurrentWorkTaskMutation mutation:
                    return (IMutationResult)Mutate(mutation);
            }

            throw new ArgumentException("No implementation found for mutation command: " + mutationCommand.GetType().FullName);
        }


        private ProjectsAndWorkTasksQueryResult Query(ProjectsAndWorkTasksQuery query)
        {
            return new ProjectsAndWorkTasksQueryResult(_data.Projects.Values.ToList());
        }

        private UserWorkContextQueryResult Query(UserWorkContextQuery query)
        {
            return new UserWorkContextQueryResult(_data.GetUserWorkContext(query.UserName));
        }

        private SetUserCurrentWorkTaskMutationResult Mutate(SetUserCurrentWorkTaskMutation mutation)
        {
            return new SetUserCurrentWorkTaskMutationResult(_data.SetUserCurrentWorkTask(mutation.UserName, mutation.WorkTaskId));
        }
        
    }
    */
}
