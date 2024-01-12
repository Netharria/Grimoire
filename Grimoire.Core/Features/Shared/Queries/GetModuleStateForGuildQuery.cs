// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Enums;

namespace Grimoire.Core.Features.Shared.Queries;

public sealed record GetModuleStateForGuildQuery : IRequest<bool>
{
    public ulong GuildId { get; init; }
    public Module Module { get; init; }
}

public sealed class GetModuleStateForGuildQueryHandler(IGrimoireDbContext grimoireDbContext) : IRequestHandler<GetModuleStateForGuildQuery, bool>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<bool> Handle(GetModuleStateForGuildQuery request, CancellationToken cancellationToken)
    {
        var query = this._grimoireDbContext.Guilds.AsNoTracking().WhereIdIs(request.GuildId);
        return request.Module switch
        {
            Module.Leveling => await query.Select(x => x.LevelSettings.ModuleEnabled).FirstAsync(cancellationToken: cancellationToken),
            Module.UserLog => await query.Select(x => x.UserLogSettings.ModuleEnabled).FirstAsync(cancellationToken: cancellationToken),
            Module.Moderation => await query.Select(x => x.ModerationSettings.ModuleEnabled).FirstAsync(cancellationToken: cancellationToken),
            Module.MessageLog => await query.Select(x => x.MessageLogSettings.ModuleEnabled).FirstAsync(cancellationToken: cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, message: null)
        };
    }
}
