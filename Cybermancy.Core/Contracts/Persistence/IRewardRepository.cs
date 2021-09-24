// -----------------------------------------------------------------------
// <copyright file="IRewardRepository.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Contracts.Persistence
{
    using Cybermancy.Domain;

    public interface IRewardRepository : IAsyncRepository<Reward>
    {
        bool Exists(ulong roleId);
    }
}