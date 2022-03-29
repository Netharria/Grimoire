// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class IIdentifiableDatabaseQueryHelper
    {
        public static IQueryable<TSource> WhereIdsAre<TSource>(this IQueryable<TSource> identifiables, ulong[] ids) where TSource : IIdentifiable
            => identifiables.Where(x => ids.Contains(x.Id));

        public static IQueryable<TSource> WhereIdIs<TSource>(this IQueryable<TSource> identifiables, ulong id) where TSource : IIdentifiable
            => identifiables.Where(x => x.Id == id);
    }
}
