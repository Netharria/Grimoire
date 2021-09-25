// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.Contracts.Persistence
{
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