// -----------------------------------------------------------------------
// <copyright file="IUserService.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Cybermancy.Domain;
using DSharpPlus.Entities;

namespace Cybermancy.Core.Contracts.Services
{
    public interface IUserService
    {
        // Task<bool> IsUserIgnored(DiscordMember member);
        /// <summary>
        ///
        /// </summary>
        /// <param name="member"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<User> GetUserAsync(DiscordMember member);

        /// <summary>
        ///
        /// </summary>
        /// <param name="user"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<User> SaveAsync(User user);
    }
}