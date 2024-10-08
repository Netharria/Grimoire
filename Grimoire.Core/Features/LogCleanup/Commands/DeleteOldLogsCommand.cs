// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.LogCleanup.Commands;

public sealed record DeleteOldLogsCommand : ICommand
{
}

public sealed class DeleteOldLogsCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<DeleteOldLogsCommand>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(DeleteOldLogsCommand command, CancellationToken cancellationToken)
    {
        var oldDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(31);
        await this._grimoireDbContext.Messages
            .Where(x => x.CreatedTimestamp <= oldDate)
            .ExecuteDeleteAsync(cancellationToken);

        await this._grimoireDbContext.Avatars
            .Select(x => new { x.GuildId, x.UserId })
            .Distinct()
            .SelectMany(x => this._grimoireDbContext.Avatars
                .Where(y => y.UserId == x.UserId && y.GuildId == x.GuildId)
                .OrderByDescending(x => x.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(cancellationToken);

        await this._grimoireDbContext.NicknameHistory
            .Select(x => new { x.GuildId, x.UserId })
            .Distinct()
            .SelectMany(x => this._grimoireDbContext.NicknameHistory
                .Where(y => y.UserId == x.UserId && y.GuildId == x.GuildId)
                .OrderByDescending(x => x.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(cancellationToken);

        await this._grimoireDbContext.UsernameHistory
            .Select(x => x.UserId)
            .Distinct()
            .SelectMany(x => this._grimoireDbContext.UsernameHistory
                .Where(y => y.UserId == x)
                .OrderByDescending(x => x.Timestamp)
                .Skip(3).ToList())
            .ExecuteDeleteAsync(cancellationToken);

        return Unit.Value;
    }
}
