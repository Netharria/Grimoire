// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Cybermancy.Core.Enums;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    /// <summary>
    /// Service class that manages the level settings in the database.
    /// </summary>
    public interface ILevelSettingsService
    {
        /// <summary>
        /// Sends a log to the leveling log channel, if one is configured for the provided guild.
        /// </summary>
        /// <param name="guildId">The guild to send the leveling log to.</param>
        /// <param name="color">The color for the embed.</param>
        /// <param name="message">The body of the embed.</param>
        /// <param name="title">The title of the embed.</param>
        /// <param name="footer">The footer of the embed.</param>
        /// <param name="embed">An embed if a custom one is required.</param>
        /// <param name="timeStamp">A timestamp to show in the embed. If none is provided then current utc is used.</param>
        /// <returns>The completed task.</returns>
        Task SendLevelingLogAsync(ulong guildId, CybermancyColor color, string message = null, string title = null,
            string footer = null, DiscordEmbed embed = null, DateTime? timeStamp = null);

        /// <summary>
        /// Updates the guild level settings in the database. It does not create a new entry. <see cref="IGuildService"/> creates an entry into this table when the guild is added.
        /// </summary>
        /// <param name="guildLevelSettings">The guild level settings to update.</param>
        /// <returns>The updated <see cref="GuildLevelSettings"/>.</returns>
        Task<GuildLevelSettings> UpdateAsync(GuildLevelSettings guildLevelSettings);

        /// <summary>
        /// Checks if leveling is enabled in this guild.
        /// </summary>
        /// <param name="guildId">The guild to check if leveling is enabled for.</param>
        /// <returns>The boolean representing if leveling is enabled.</returns>
        Task<bool> IsLevelingEnabledAsync(ulong guildId);

        /// <summary>
        /// Gets the guild level settings for the provided guild id.
        /// </summary>
        /// <param name="guildId">The guild id to find the <see cref="GuildLevelSettings"/> for.</param>
        /// <returns>The requested <see cref="GuildLevelSettings"/></returns>
        ValueTask<GuildLevelSettings> GetGuildAsync(ulong guildId);
    }
}
