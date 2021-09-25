// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    /// <summary>
    /// Service class that manages different user interactions between discord and the database.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Gets the <see cref="User"/> that corresponds to the provided <see cref="DiscordMember"/>. If one does not exist, creates a new one and returns the result.
        /// </summary>
        /// <param name="member">The <see cref="DiscordMember"/></param>
        /// <returns>The requested <see cref="User"/>.</returns>
        Task<User> GetOrCreateUserAsync(DiscordMember member);

        /// <summary>
        /// Gets the <see cref="User"/> that corresponds to the provided <see cref="DiscordUser"/>. If one does not exist, creates a new one and returns the result.
        /// </summary>
        /// <param name="member">The <see cref="DiscordUser"/></param>
        /// <returns>The requested <see cref="User"/>.</returns>
        Task<User> GetOrCreateUserAsync(DiscordUser user, ulong guildId);

        /// <summary>
        /// Saves or Updates the provided <see cref="User"/> in the database.
        /// </summary>
        /// <param name="user">The <see cref="User"/> to save.</param>
        /// <returns>The saved <see cref="User"/>.</returns>
        Task<User> SaveAsync(User user);
    }
}
