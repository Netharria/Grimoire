using System;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;

namespace Cybermancy.Core.Services
{
    public class UserLevelService : IUserLevelService
    {
        private readonly IUserLevelRepository _userLevelRepository;
        private readonly IAsyncIdRepository<User> _userRepository;

        public UserLevelService(IUserLevelRepository userLevelRepository)
        {
            _userLevelRepository = userLevelRepository;
        }

        public async Task<UserLevel> GetUserLevels(ulong userId, ulong guildId)
        {
            if (_userLevelRepository.Exists(userId, guildId)) return await _userLevelRepository.GetUserLevel(userId, guildId);
            var newUserLevel = new UserLevel()
            {
                GuildId = guildId,
                UserId = userId,
                TimeOut = DateTime.UtcNow,
                Xp = 0
            };
            return await Save(newUserLevel);
        }

        public async Task<UserLevel> Save(UserLevel userLevel)
        {
            if (_userLevelRepository.Exists(userLevel.UserId, userLevel.GuildId))
                return await _userLevelRepository.UpdateAsync(userLevel);
            return await _userLevelRepository.AddAsync(userLevel);
        }
    }
}