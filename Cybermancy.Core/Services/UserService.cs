using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;
using System.Threading.Tasks;

namespace Cybermancy.Core.Services
{
    public class UserService : IUserService
    {

        private readonly IAsyncIdRepository<User> _userRepository;
        private readonly IGuildService _guildService;

        public UserService(IAsyncIdRepository<User> userRepository, IGuildService guildService)
        {
            _userRepository = userRepository;
            _guildService = guildService;
        }
        public async Task<User> GetUser(DiscordMember member)
        {
            if (await _userRepository.Exists(member.Id)) return await _userRepository.GetByIdAsync(member.Id);
            var newUser = new User()
            {
                Id = member.Id,
                UserName = $"{member.Username}#{member.Discriminator}",
                DisplayName = member.DisplayName,
                AvatarUrl = member.AvatarUrl
            };
            newUser.Guilds.Add(await _guildService.GetGuild(member.Guild.Id));
            return await Save(newUser);
        }
        public async Task<User> Save(User user)
        {
            if (await _userRepository.Exists(user.Id))
                return await _userRepository.UpdateAsync(user);
            return await _userRepository.AddAsync(user);
        }
    }
}