// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// ReSharper disable AccessToDisposedClosure
// Action handled immediately after creation
namespace Grimoire.Features.LogCleanup.Commands;

public sealed record DeleteOldLogsCommand : IRequest
{
}

public sealed class DeleteOldLogsCommandHandler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    : IRequestHandler<DeleteOldLogsCommand>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

    public async Task Handle(DeleteOldLogsCommand command, CancellationToken cancellationToken)
    {
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
        var oldDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(31);
        await dbContext.Messages
            .Where(x => x.CreatedTimestamp <= oldDate)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Avatars
            .Select(x => new { x.GuildId, x.UserId })
            .Distinct()
            .SelectMany((x) => dbContext.Avatars
                .Where(avatar => avatar.UserId == x.UserId && avatar.GuildId == x.GuildId)
                .OrderByDescending(avatar => avatar.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.NicknameHistory
            .Select(x => new { x.GuildId, x.UserId })
            .Distinct()
            .SelectMany(x => dbContext.NicknameHistory
                .Where(y => y.UserId == x.UserId && y.GuildId == x.GuildId)
                .OrderByDescending(nicknameHistory => nicknameHistory.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.UsernameHistory
            .Select(x => x.UserId)
            .Distinct()
            .SelectMany(x => dbContext.UsernameHistory
                .Where(y => y.UserId == x)
                .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(cancellationToken);
    }
}
