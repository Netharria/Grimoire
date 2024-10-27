// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Commands;

public sealed record UnlockChannelCommand : IRequest<UnlockChannelCommandResponse>
{
    public ulong ChannelId { get; init; }

    public ulong GuildId { get; init; }
}

public sealed class UnlockChannelCommandHandler(GrimoireDbContext grimoireDbContext)
    : IRequestHandler<UnlockChannelCommand, UnlockChannelCommandResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<UnlockChannelCommandResponse> Handle(UnlockChannelCommand command,
        CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Locks
            .Where(x => x.ChannelId == command.ChannelId && x.GuildId == command.GuildId)
            .Select(x => new { Lock = x, ModerationLogId = x.Guild.ModChannelLog })
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null || result.Lock is null)
            throw new AnticipatedException("Could not find a lock entry for that channel.");

        this._grimoireDbContext.Locks.Remove(result.Lock);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

        return new UnlockChannelCommandResponse
        {
            LogChannelId = result.ModerationLogId,
            PreviouslyAllowed = result.Lock.PreviouslyAllowed,
            PreviouslyDenied = result.Lock.PreviouslyDenied
        };
    }
}

public sealed record UnlockChannelCommandResponse : BaseResponse
{
    public long PreviouslyAllowed { get; init; }
    public long PreviouslyDenied { get; init; }
}
