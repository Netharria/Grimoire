// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class UserLevelService : IUserLevelService
    {
        private readonly IUserLevelRepository _userLevelRepository;
        private readonly IAsyncIdRepository<Guild> _guildRepository;
        private readonly IUserService _userService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLevelService"/> class.
        /// </summary>
        /// <param name="userLevelRepository"></param>
        public UserLevelService(IUserLevelRepository userLevelRepository, IAsyncIdRepository<Guild> guildRepository, IUserService userService)
        {
            this._userLevelRepository = userLevelRepository;
            this._guildRepository = guildRepository;
            this._userService = userService;
        }

        public async Task<UserLevel> GetUserLevelAsync(ulong userId, ulong guildId)
        {
            var result = await this._userLevelRepository.GetUserLevelAsync(userId, guildId);
            if (result is not null)
                return result;
            await this.AddUserAsync(userId, guildId);
            return await this.GetUserLevelAsync(userId, guildId);
        }

        public async Task<UserLevel> GetOrCreateUserLevelAsync(DiscordUser user, ulong guildId)
        {
            var result = await this._userLevelRepository.GetUserLevelAsync(user.Id, guildId);
            if (result is not null)
                return result;
            _ = await this._userService.GetOrCreateUserAsync(user, guildId);
            await this.AddUserAsync(user.Id, guildId);
            return await this.GetUserLevelAsync(user.Id, guildId);
        }

        public async Task<UserLevel> SaveAsync(UserLevel userLevel)
        {
            if (await this._userLevelRepository.Exists(userLevel.UserId, userLevel.GuildId))
                return await this._userLevelRepository.UpdateAsync(userLevel);
            return await this._userLevelRepository.AddAsync(userLevel);
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

        public async Task<ICollection<UserLevel>> GetAllIgnoredUsersAsync(ulong guildId)
        {
            var guild = await this._guildRepository.GetByIdAsync(guildId);
            if (guild is null) throw new ArgumentNullException(nameof(guildId));
            var userLevels = guild.UserLevels.Where(x => x.IsXpIgnored).ToList();
            return userLevels;
        }
    }
}
