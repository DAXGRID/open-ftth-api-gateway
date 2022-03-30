using OpenFTTH.CQRS;
using OpenFTTH.Work.API.Mutations;
using System.Threading.Tasks;

namespace OpenFTTH.Work.Business.InMemTestImpl
{
    public class SetUserCurrentWorkTaskCommandHandler :
        ICommandHandler<SetUserCurrentWorkTaskMutation, SetUserCurrentWorkTaskMutationResult>
    {
        private readonly InMemRepoImpl _data;

        public SetUserCurrentWorkTaskCommandHandler(InMemRepoImpl data)
        {
            _data = data;
        }

        public Task<SetUserCurrentWorkTaskMutationResult> HandleAsync(SetUserCurrentWorkTaskMutation command)
        {
            return Task.FromResult<SetUserCurrentWorkTaskMutationResult>(
                new SetUserCurrentWorkTaskMutationResult(_data.SetUserCurrentWorkTask(command.UserName, command.WorkTaskId))
            );
        }
    }
}
