// -----------------------------------------------------------------------
// <copyright file="UserService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Services
{
    using System.Threading.Tasks;
    using Cybermancy.Core.Contracts.Persistence;
    using Cybermancy.Core.Contracts.Services;
    using Cybermancy.Domain;
    using DSharpPlus.Entities;

    public class UserService : IUserService
    {
        private readonly IAsyncIdRepository<User> userRepository;
        private readonly IGuildService guildService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="userRepository"></param>
        /// <param name="guildService"></param>
        public UserService(IAsyncIdRepository<User> userRepository, IGuildService guildService)
        {
            this.userRepository = userRepository;
            this.guildService = guildService;
        }

        public async Task<User> GetUserAsync(DiscordMember member)
        {
            if (await this.userRepository.ExistsAsync(member.Id)) return await this.userRepository.GetByIdAsync(member.Id);
            var newUser = new User()
            {
                Id = member.Id,
                UserName = $"{member.Username}#{member.Discriminator}",
                DisplayName = member.DisplayName,
                AvatarUrl = member.AvatarUrl,
            };
            newUser.Guilds.Add(await this.guildService.GetGuildAsync(member.Guild.Id));
            return await this.SaveAsync(newUser);
        }

        public async Task<User> SaveAsync(User user)
        {
            if (await this.userRepository.ExistsAsync(user.Id))
                return await this.userRepository.UpdateAsync(user);
            return await this.userRepository.AddAsync(user);
        }
    }
}