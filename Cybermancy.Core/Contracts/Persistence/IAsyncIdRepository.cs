using Cybermancy.Domain.Shared;
using System.Threading.Tasks;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IAsyncIdRepository<T> : IAsyncRepository<T> where T : Identifiable
    {
        Task<T> GetByIdAsync(ulong id);
        bool Exists(ulong id);
    }
}
