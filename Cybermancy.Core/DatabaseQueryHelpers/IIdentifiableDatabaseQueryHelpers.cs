// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Core.DatabaseQueryHelpers
{
    public static class IIdentifiableDatabaseQueryHelpers
    {
        public static IQueryable<TSource> WhereIdsAre<TSource, T>(this IQueryable<TSource> identifiables, T[] ids)
            where TSource : IIdentifiable<T>
            where T : unmanaged, IComparable, IEquatable<T>
            => identifiables.Where(x => ids.Contains(x.Id));

        public static IQueryable<TSource> WhereIdIs<TSource, T>(this IQueryable<TSource> identifiables, T id)
            where TSource : IIdentifiable<ulong>
            where T : unmanaged, IComparable, IEquatable<T>
            => identifiables.Where(x => x.Id.Equals(id));
    }
}
