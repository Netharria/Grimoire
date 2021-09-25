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
    /// Service class that manages different guild interactions between discord and the database.
    /// </summary>
    public interface IGuildService
    {
        /// <summary>
        /// Gets the guild from the database. If it does not exist, a new entry is created.
        /// </summary>
        /// <param name="guild">The guild to retrieve.</param>
        /// <returns>The requested guild.</returns>
        ValueTask<Guild> GetOrCreateGuildAsync(DiscordGuild guild);
        /// <summary>
        /// Gets the guild with the provided id from the database.
        /// </summary>
        /// <param name="guildId">The id of the guild to retrieve</param>
        /// <returns>The requested guild.</returns>
        ValueTask<Guild> GetGuildAsync(ulong guildId);

        /// <summary>
        /// Creates entries for all the provided guilds.
        /// </summary>
        /// <param name="guilds">The guilds to add.</param>
        /// <returns>The complted task.</returns>
        Task SetupAllGuildAsync(IEnumerable<DiscordGuild> guilds);
    }
}
