// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Linq;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain;

namespace Cybermancy.Persistence.Repositories
{
    public class RewardRepository : BaseRepository<Reward>, IRewardRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RewardRepository"/> class.
        /// </summary>
        /// <param name="cybermancyDb"></param>
        public RewardRepository(CybermancyDbContext cybermancyDb)
            : base(cybermancyDb)
        {
        }

        public bool Exists(ulong roleId) => this.CybermancyDb.Rewards.Any(x => x.RoleId == roleId);
    }
}
