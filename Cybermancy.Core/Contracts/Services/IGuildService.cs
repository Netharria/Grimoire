// -----------------------------------------------------------------------
// <copyright file="IGuildService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IGuildService
    {
        ValueTask<Guild> GetGuildAsync(DiscordGuild guild);

        ValueTask<Guild> GetGuildAsync(ulong guildId);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guilds"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task SetupAllGuildAsync(IEnumerable<DiscordGuild> guilds);
    }
}