// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Settings;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Grimoire.Extensions;

public static class DbContextFactoryExtension
{
    public static Eff<TResult> StartTransaction<TContext, TResult>(
        this IDbContextFactory<TContext> dbContextFactory,
        Func<TContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) where TContext : DbContext =>
        from dbContext in useAsync(liftIO(() => dbContextFactory.CreateDbContextAsync(cancellationToken)))
        from result in liftEff(() => operation(dbContext, cancellationToken))
        from _ in release(dbContext)
        select result;

    public static Eff<TResult> StartTransaction<TContext, TResult>(
        this IDbContextFactory<TContext> dbContextFactory,
        Func<TContext, CancellationToken, Eff<TResult>> operation,
        CancellationToken cancellationToken = default) where TContext : DbContext =>
        from dbContext in useAsync(liftIO(() => dbContextFactory.CreateDbContextAsync(cancellationToken)))
        from result in operation(dbContext, cancellationToken)
        from _ in release(dbContext)
        select result;
}
