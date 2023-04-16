// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Core.DatabaseQueryHelpers
{
    public static class IXpIgnoreDatabaseQueryHelpers
    {
        public static IQueryable<TSource> WhereIgnored<TSource>(this IQueryable<TSource> ignorable) where TSource : IXpIgnore
            => ignorable.Where(x => x.IsXpIgnored);

        public static IQueryable<TSource> WhereIgnored<TSource>(this IQueryable<TSource> ignorable, bool ignored) where TSource : IXpIgnore
            => ignorable.Where(x => x.IsXpIgnored == ignored);
    }
}
