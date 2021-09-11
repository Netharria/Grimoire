using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IGuildService
    {
        Task<Guild> GetGuildAndSetupIfDoesntExist(DiscordGuild guild);
    }
}