using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IRoleService
    {
        bool AreAnyRolesIgnored(ICollection<DiscordRole> roles, DiscordGuild guild);
        Task<Role> Save(Role role);
        Task<Role> GetRole(DiscordRole role, DiscordGuild guild);
        Task<Role> GetRole(ulong roleId);
        Task SetupAllRoles(IEnumerable<DiscordGuild> guilds);
    }
}