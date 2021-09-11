using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class RoleService : IRoleService
    {
        
        private readonly IAsyncIdRepository<Role> _roleRepository;
        private readonly IGuildService _guildService;

        public RoleService(IAsyncIdRepository<Role> roleRepository, IGuildService guildService)
        {
            _roleRepository = roleRepository;
            _guildService = guildService;
        }

        public bool AreAnyRolesIgnored(ICollection<DiscordRole> roles, DiscordGuild guild)
        {
            var databaseRoles = roles
                .Select(x => GetRole(x, guild).Result).ToList();
            return databaseRoles.Any(x => x.IsXpIgnored);
        }

        public async Task<Role> Save(Role role)
        {
            if (await _roleRepository.Exists(role.Id))
                return await _roleRepository.UpdateAsync(role);
            return await _roleRepository.AddAsync(role);
        }

        public async Task<Role> GetRole(DiscordRole role, DiscordGuild guild)
        {
            if(await _roleRepository.Exists(role.Id)) return await _roleRepository.GetByIdAsync(role.Id);
            var newRole = new Role()
            {
                Guild = await _guildService.GetGuildAndSetupIfDoesntExist(guild),
                Id = role.Id
            };
            return await Save(newRole);
        }
    }
}