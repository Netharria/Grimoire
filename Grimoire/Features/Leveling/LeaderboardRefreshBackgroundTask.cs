// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.LogCleanup;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.Leveling;

internal sealed partial class LeaderboardRefreshBackgroundTask(
    IServiceProvider serviceProvider,
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    ILogger<LeaderboardRefreshBackgroundTask> logger)
    : GenericBackgroundService(serviceProvider, logger, TimeSpan.FromMinutes(15))
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    protected override async Task RunTask(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        LogLeaderboardRefresh(logger);
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "REFRESH MATERIALIZED VIEW CONCURRENTLY leaderboard_view",
            cancellationToken);
    }

    [LoggerMessage(LogLevel.Warning, "Refreshing leaderboard")]
    static partial void LogLeaderboardRefresh(ILogger logger);
}
