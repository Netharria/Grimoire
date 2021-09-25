// -----------------------------------------------------------------------
// <copyright file="UserLevelService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

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

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLevelService"/> class.
        /// </summary>
        /// <param name="userLevelRepository"></param>
        public UserLevelService(IUserLevelRepository userLevelRepository)
        {
            this._userLevelRepository = userLevelRepository;
        }

        public async Task<UserLevel> GetUserLevelAsync(ulong userId, ulong guildId)
        {
            var result = await this._userLevelRepository.GetUserLevelAsync(userId, guildId);
            if (result is not null)
                return result;
            await this.AddUserAsync(userId, guildId);
            return await this.GetUserLevelAsync(userId, guildId);
        }

        public Task<UserLevel> SaveAsync(UserLevel userLevel)
        {
            if (this._userLevelRepository.Exists(userLevel.UserId, userLevel.GuildId))
                return this._userLevelRepository.UpdateAsync(userLevel);
            return this._userLevelRepository.AddAsync(userLevel);
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

        public Task<IList<UserLevel>> GetRankedUsersAsync(ulong guildId) => this._userLevelRepository.GetRankedGuildUsersAsync(guildId);

        public Task<ICollection<UserLevel>> GetAllIgnoredUsersAsync(ulong guildId) => throw new NotImplementedException();
    }
}
