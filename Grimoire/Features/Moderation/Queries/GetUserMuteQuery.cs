// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Queries;

public sealed record GetUserMuteQuery : IQuery<ulong?>
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
}

public sealed class GetUserMuteQueryHandler(GrimoireDbContext grimoireDbContext) : IQueryHandler<GetUserMuteQuery, ulong?>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<ulong?> Handle(GetUserMuteQuery query, CancellationToken cancellationToken)
        => await this._grimoireDbContext.Mutes
            .AsNoTracking()
            .WhereMemberHasId(query.UserId, query.GuildId)
            .Where(x => x.Guild.ModerationSettings.ModuleEnabled)
            .Select(x =>
                x.Guild.ModerationSettings.MuteRole)
            .FirstOrDefaultAsync(cancellationToken);
}
