// -----------------------------------------------------------------------
// <copyright file="IChannelService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Contracts.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cybermancy.Domain;
    using DSharpPlus.Entities;

    public interface IChannelService
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="discordChannel"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<bool> IsChannelIgnoredAsync(DiscordChannel discordChannel);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<ICollection<Channel>> GetAllIgnoredChannelsAsync(ulong guildId);

        /// <summary>
        ///
        /// </summary>
        /// <param name="channel"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<Channel> SaveAsync(Channel channel);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guilds"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task SetupAllChannelsAsync(IEnumerable<DiscordGuild> guilds);

        ValueTask<Channel> GetChannelAsync(ulong id);

        /// <summary>
        ///
        /// </summary>
        /// <param name="discordChannel"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<Channel> GetChannelAsync(DiscordChannel discordChannel);
    }
}