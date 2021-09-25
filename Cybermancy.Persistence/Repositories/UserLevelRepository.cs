// -----------------------------------------------------------------------
// <copyright file="UserLevelRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain;

namespace Cybermancy.Persistence.Repositories
{
    public class UserLevelRepository : BaseRepository<UserLevel>, IUserLevelRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserLevelRepository"/> class.
        /// </summary>
        /// <param name="cybermancyDb"></param>
        public UserLevelRepository(CybermancyDbContext cybermancyDb)
            : base(cybermancyDb)
        {
        }

        public Task<UserLevel> GetUserLevelAsync(ulong userId, ulong guildId)
        {
            var result = this.CybermancyDb.UserLevels.FirstOrDefault(x => x.UserId == userId && x.GuildId == guildId);
            return Task.FromResult(result);
        }

        public bool Exists(ulong userId, ulong guildId) => this.CybermancyDb.UserLevels.Any(x => x.UserId == userId && x.GuildId == guildId);

        public Task<IList<UserLevel>> GetRankedGuildUsersAsync(ulong guildId)
        {
            IList<UserLevel> result = this.CybermancyDb.UserLevels.Where(x => x.GuildId == guildId).OrderByDescending(x => x.Xp).ToList();
            return Task.FromResult(result);
        }
    }
}