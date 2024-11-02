// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Mute;

public sealed record GetUserMuteQuery : IRequest<ulong?>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class GetUserMuteQueryHandler(GrimoireDbContext grimoireDbContext)
    : IRequestHandler<GetUserMuteQuery, ulong?>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<ulong?> Handle(GetUserMuteQuery query, CancellationToken cancellationToken)
        => await this._grimoireDbContext.Mutes
            .AsNoTracking()
            .WhereMemberHasId(query.UserId, query.GuildId)
            .Where(x => x.Guild.ModerationSettings.ModuleEnabled)
            .Select(x =>
                x.Guild.ModerationSettings.MuteRole)
            .FirstOrDefaultAsync(cancellationToken);
}
