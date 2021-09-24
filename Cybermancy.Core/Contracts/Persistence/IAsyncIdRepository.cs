// -----------------------------------------------------------------------
// <copyright file="IAsyncIdRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Contracts.Persistence
{
    using System.Threading.Tasks;
    using Cybermancy.Domain.Shared;

    public interface IAsyncIdRepository<T> : IAsyncRepository<T>
        where T : Identifiable
    {
        ValueTask<T> GetByIdAsync(ulong id);

        /// <summary>
        ///
        /// </summary>
        /// <param name="id"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        Task<bool> ExistsAsync(ulong id);
    }
}