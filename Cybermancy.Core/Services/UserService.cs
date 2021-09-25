// -----------------------------------------------------------------------
// <copyright file="UserService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Core.Contracts.Services;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Services
{
    public class UserService : IUserService
    {
        private readonly IAsyncIdRepository<User> _userRepository;
        private readonly IGuildService _guildService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="userRepository"></param>
        /// <param name="guildService"></param>
        public UserService(IAsyncIdRepository<User> userRepository, IGuildService guildService)
        {
            this._userRepository = userRepository;
            this._guildService = guildService;
        }

        public async Task<User> GetUserAsync(DiscordMember member)
        {
            if (await this._userRepository.ExistsAsync(member.Id)) return await this._userRepository.GetByIdAsync(member.Id);
            var newUser = new User()
            {
                Id = member.Id,
                UserName = $"{member.Username}#{member.Discriminator}",
                DisplayName = member.DisplayName,
                AvatarUrl = member.AvatarUrl,
            };
            newUser.Guilds.Add(await this._guildService.GetGuildAsync(member.Guild.Id));
            return await this.SaveAsync(newUser);
        }

        public async Task<User> SaveAsync(User user)
        {
            if (await this._userRepository.ExistsAsync(user.Id))
                return await this._userRepository.UpdateAsync(user);
            return await this._userRepository.AddAsync(user);
        }
    }
}
