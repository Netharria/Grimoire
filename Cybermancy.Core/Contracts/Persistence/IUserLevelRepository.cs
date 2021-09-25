// -----------------------------------------------------------------------
// <copyright file="IUserLevelRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IUserLevelRepository : IAsyncRepository<UserLevel>
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<UserLevel> GetUserLevelAsync(ulong userId, ulong guildId);

        bool Exists(ulong userId, ulong guildId);

        /// <summary>
        ///
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<IList<UserLevel>> GetRankedGuildUsersAsync(ulong guildId);
    }
}