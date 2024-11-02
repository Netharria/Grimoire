// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace Grimoire.Features.Moderation.Mute;

public sealed record GetExpiredMutesQuery : IStreamRequest<GetExpiredMutesQueryResponse>
{
}

public sealed class GetExpiredMutesQueryHandler(GrimoireDbContext grimoireDbContext)
    : IStreamRequestHandler<GetExpiredMutesQuery, GetExpiredMutesQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async IAsyncEnumerable<GetExpiredMutesQueryResponse> Handle(GetExpiredMutesQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var mute in this._grimoireDbContext.Mutes
                           .AsNoTracking()
                           .Where(x => x.EndTime < DateTimeOffset.UtcNow)
                           .Where(x => x.Guild.ModerationSettings.MuteRole != null)
                           .Select(x => new GetExpiredMutesQueryResponse
                           {
                               UserId = x.UserId,
                               GuildId = x.GuildId,
                               MuteRole = x.Guild.ModerationSettings.MuteRole!.Value,
                               LogChannelId = x.Guild.ModChannelLog
                           }).AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return mute;
    }
}

public sealed record GetExpiredMutesQueryResponse : BaseResponse
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
    public ulong MuteRole { get; init; }
}
