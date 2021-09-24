// -----------------------------------------------------------------------
// <copyright file="UserLevelService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cybermancy.Core.Contracts.Persistence;
    using Cybermancy.Core.Contracts.Services;
    using Cybermancy.Domain;

    public class UserLevelService : IUserLevelService
    {
        private readonly IUserLevelRepository userLevelRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLevelService"/> class.
        /// </summary>
        /// <param name="userLevelRepository"></param>
        public UserLevelService(IUserLevelRepository userLevelRepository)
        {
            this.userLevelRepository = userLevelRepository;
        }

        public async Task<UserLevel> GetUserLevelAsync(ulong userId, ulong guildId)
        {
            var result = await this.userLevelRepository.GetUserLevelAsync(userId, guildId);
            if (result is not null)
                return result;
            await this.AddUserAsync(userId, guildId);
            return await this.GetUserLevelAsync(userId, guildId);
        }

        public Task<UserLevel> SaveAsync(UserLevel userLevel)
        {
            if (this.userLevelRepository.Exists(userLevel.UserId, userLevel.GuildId))
                return this.userLevelRepository.UpdateAsync(userLevel);
            return this.userLevelRepository.AddAsync(userLevel);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public Task<UserLevel> AddUserAsync(ulong userId, ulong guildId)
        {
            var newUserLevel = new UserLevel()
            {
                GuildId = guildId,
                UserId = userId,
                TimeOut = DateTime.UtcNow,
                Xp = 0,
            };
            return this.SaveAsync(newUserLevel);
        }

        public Task<IList<UserLevel>> GetRankedUsersAsync(ulong guildId)
        {
            return this.userLevelRepository.GetRankedGuildUsersAsync(guildId);
        }

        public Task<ICollection<UserLevel>> GetAllIgnoredUsersAsync(ulong guildId)
        {
            throw new NotImplementedException();
        }
    }
}