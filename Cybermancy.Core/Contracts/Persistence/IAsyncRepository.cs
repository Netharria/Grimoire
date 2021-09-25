// -----------------------------------------------------------------------
// <copyright file="IAsyncRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cybermancy.Core.Contracts.Persistence
{
    public interface IAsyncRepository<T>
        where T : class
    {
        /// <summary>
        ///
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<IReadOnlyList<T>> GetAllAsync();

        /// <summary>
        ///
        /// </summary>
        /// <param name="keys"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        ValueTask<T> GetByPrimaryKeyAsync(params object[] keys);

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<T> AddAsync(T entity);

        /// <summary>
        ///
        /// </summary>
        /// <param name="entities"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<ICollection<T>> AddMultipleAsync(ICollection<T> entities);

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<T> UpdateAsync(T entity);

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        Task DeleteAsync(T entity);
    }
}