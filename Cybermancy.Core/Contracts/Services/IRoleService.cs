using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IRoleService
    {
        Task<bool> AreAnyRolesIgnored(ICollection<DiscordRole> roles);
        Task<Role> Save(Role role);
    }
}