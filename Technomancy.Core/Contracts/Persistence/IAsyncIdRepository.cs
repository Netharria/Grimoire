using Technomancy.Domain.Shared;
using System.Threading.Tasks;

namespace Technomancy.Core.Contracts.Persistence
{
    public interface IAsyncIdRepository<T> : IAsyncRepository<T> where T : Identifiable
    {
        Task<T> GetByIdAsync(ulong id);
        bool Exists(ulong id);
    }
}
