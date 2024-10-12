// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Numerics;
using Grimoire.Domain.Shared;

namespace Grimoire.DatabaseQueryHelpers;

public static class IIdentifiableDatabaseQueryHelpers
{
    public static IQueryable<TSource> WhereIdsAre<TSource, T>(this IQueryable<TSource> identifiables, T[] ids)
        where TSource : IIdentifiable<T>
        where T : IBinaryInteger<T>
        => identifiables.Where(x => ids.Contains(x.Id));

    public static IQueryable<TSource> WhereIdIs<TSource, T>(this IQueryable<TSource> identifiables, T id)
        where TSource : IIdentifiable<T>
        where T : IBinaryInteger<T>
        => identifiables.Where(x => x.Id.Equals(id));
}
