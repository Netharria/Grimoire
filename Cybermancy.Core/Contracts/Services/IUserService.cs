using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IUserService
    {
        //Task<bool> IsUserIgnored(DiscordMember member);
        Task<User> GetUser(DiscordMember member);
        Task<User> Save(User user);
    }
}