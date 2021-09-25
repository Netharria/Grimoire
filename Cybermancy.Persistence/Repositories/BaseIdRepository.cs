// -----------------------------------------------------------------------
// <copyright file="BaseIdRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Persistence.Repositories
{
    public class BaseIdRepository<T> : BaseRepository<T>, IAsyncIdRepository<T>
        where T : Identifiable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseIdRepository{T}"/> class.
        /// </summary>
        /// <param name="cybermancyDb"></param>
        public BaseIdRepository(CybermancyDbContext cybermancyDb)
            : base(cybermancyDb)
        {
        }

        public Task<bool> ExistsAsync(ulong id) => Task.FromResult(this.CybermancyDb.Set<T>().Any(x => x.Id == id));

        public virtual ValueTask<T> GetByIdAsync(ulong id) => this.CybermancyDb.Set<T>().FindAsync(id);
    }
}