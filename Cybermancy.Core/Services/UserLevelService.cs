using System;
using System.Collections.Generic;
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

        public async Task<UserLevel> GetUserLevel(ulong userId, ulong guildId)
        {
            var result = await _userLevelRepository.GetUserLevel(userId, guildId);
            if (result is not null)
                return result;
            await AddUser(userId, guildId);
            return await GetUserLevel(userId, guildId);
        }

        public async Task<UserLevel> Save(UserLevel userLevel)
        {
            if (_userLevelRepository.Exists(userLevel.UserId, userLevel.GuildId))
                return await _userLevelRepository.UpdateAsync(userLevel);
            return await _userLevelRepository.AddAsync(userLevel);
        }

        public async Task<UserLevel> AddUser(ulong userId, ulong guildId)
        {
            var newUserLevel = new UserLevel()
            {
                GuildId = guildId,
                UserId = userId,
                TimeOut = DateTime.UtcNow,
                Xp = 0
            };
            return await Save(newUserLevel);
        }

        public async Task<IList<UserLevel>> GetRankedUsers(ulong guildId)
        {
            return await _userLevelRepository.GetRankedGuildUsers(guildId);
        }
    }
}