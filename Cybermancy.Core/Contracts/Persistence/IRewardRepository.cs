using Cybermancy.Domain;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IRewardRepository : IAsyncRepository<Reward>
    {
        bool Exists(ulong roleId);
    }
}