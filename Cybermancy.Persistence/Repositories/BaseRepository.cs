// -----------------------------------------------------------------------
// <copyright file="BaseRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Persistence.Repositories
{
    public class BaseRepository<T> : IAsyncRepository<T>
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseRepository{T}"/> class.
        /// </summary>
        /// <param name="cybermancyDb"></param>
        public BaseRepository(CybermancyDbContext cybermancyDb)
        {
            this.CybermancyDb = cybermancyDb;
        }

        protected CybermancyDbContext CybermancyDb { get; }

        public async Task<IReadOnlyList<T>> GetAllAsync() => await this.CybermancyDb.Set<T>().ToListAsync();

        public async Task<T> AddAsync(T entity)
        {
            await this.CybermancyDb.Set<T>().AddAsync(entity);
            await this.CybermancyDb.SaveChangesAsync();

            return entity;
        }

        public async Task<ICollection<T>> AddMultipleAsync(ICollection<T> entities)
        {
            await this.CybermancyDb.Set<T>().AddRangeAsync(entities);
            await this.CybermancyDb.SaveChangesAsync();

            return entities;
        }

        public async Task<T> UpdateAsync(T entity)
        {
            this.CybermancyDb.Entry(entity).State = EntityState.Modified;
            await this.CybermancyDb.SaveChangesAsync();
            return entity;
        }

        public Task DeleteAsync(T entity)
        {
            this.CybermancyDb.Set<T>().Remove(entity);
            return this.CybermancyDb.SaveChangesAsync();
        }

        public ValueTask<T> GetByPrimaryKeyAsync(params object[] keys) => this.CybermancyDb.Set<T>().FindAsync(keys);
    }
}