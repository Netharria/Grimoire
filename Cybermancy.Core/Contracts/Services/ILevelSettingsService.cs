// -----------------------------------------------------------------------
// <copyright file="ILevelSettingsService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Cybermancy.Core.Enums;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface ILevelSettingsService
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="guildId"></param>
        /// <param name="color"></param>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="footer"></param>
        /// <param name="embed"></param>
        /// <param name="timeStamp"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task SendLevelingLogAsync(
            ulong guildId,
            CybermancyColor color,
            string message = null,
            string title = null,
            string footer = null,
            DiscordEmbed embed = null,
            DateTime? timeStamp = null);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guildLevelSettings"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<GuildLevelSettings> UpdateAsync(GuildLevelSettings guildLevelSettings);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<bool> IsLevelingEnabledAsync(ulong guildId);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        ValueTask<GuildLevelSettings> GetGuildAsync(ulong guildId);
    }
}