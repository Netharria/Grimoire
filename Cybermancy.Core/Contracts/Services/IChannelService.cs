// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    /// <summary>
    /// Service class that manages different channel interactions between discord and the database.
    /// </summary>
    public interface IChannelService
    {
        /// <summary>
        /// Checks if the channel is ignored for xp gain.
        /// </summary>
        /// <param name="discordChannel">The channel to check the ignored status.</param>
        /// <returns>A boolean representing whether the channel is ignored.</returns>
        Task<bool> IsChannelIgnoredAsync(DiscordChannel discordChannel);

        /// <summary>
        /// Gets all channels that are ignored for the provided guild.
        /// </summary>
        /// <param name="guildId">The guild to get all the ignored channels for.</param>
        /// <returns>All channels that are ignored for xp gain in the guild.</returns>
        Task<ICollection<Channel>> GetAllIgnoredChannelsAsync(ulong guildId);

        /// <summary>
        /// Saves or updates the provided channel in the database.
        /// </summary>
        /// <param name="channel">The channel to save.</param>
        /// <returns>The saved channel.</returns>
        Task<Channel> SaveAsync(Channel channel);

        /// <summary>
        /// Creates entries for all channels for the provided guilds.
        /// </summary>
        /// <param name="guilds">The guilds to add the channels for.</param>
        /// <returns>The complted task.</returns>
        Task SetupAllChannelsAsync(IEnumerable<DiscordGuild> guilds);

        /// <summary>
        /// Gets the channel with the provided id from the database.
        /// </summary>
        /// <param name="id">The id of the channel to retrieve.</param>
        /// <returns>The requested channel.</returns>
        ValueTask<Channel> GetChannelAsync(ulong id);
        /// <summary>
        /// Gets the channel with the provided id from the database. If it does not exist, a new entry is created.
        /// </summary>
        /// <param name="discordChannel">The channel to retrieve.</param>
        /// <returns>The requested channel.</returns>
        Task<Channel> GetOrCreateChannelAsync(DiscordChannel discordChannel);
    }
}
