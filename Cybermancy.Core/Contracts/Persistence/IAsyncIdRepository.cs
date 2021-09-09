using System.Threading.Tasks;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IAsyncIdRepository<T> : IAsyncRepository<T> where T : Identifiable
    {
        Task<T> GetByIdAsync(ulong id);
        bool Exists(ulong id);
    }
}