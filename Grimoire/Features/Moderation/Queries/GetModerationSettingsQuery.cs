// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Queries;

public sealed record GetModerationSettingsQuery : IRequest<GetModerationSettingsQueryResponse>
{
    public ulong GuildId { get; init; }
}

public sealed class GetModerationSettingsQueryHandler(GrimoireDbContext context) : IRequestHandler<GetModerationSettingsQuery, GetModerationSettingsQueryResponse>
{
    private readonly GrimoireDbContext _context = context;

    public async Task<GetModerationSettingsQueryResponse> Handle(GetModerationSettingsQuery query, CancellationToken cancellationToken)
    {
        var result =  await this._context.GuildModerationSettings
            .AsNoTracking()
            .Where(x => x.GuildId == query.GuildId)
            .Select(x => new GetModerationSettingsQueryResponse
            {
                AutoPardonAfter = x.AutoPardonAfter,
                PublicBanLog = x.PublicBanLog,
                ModuleEnabled = x.ModuleEnabled
            }).FirstOrDefaultAsync(cancellationToken);

        if (result is null) throw new AnticipatedException("No settings were found for this server.");

        return result;
    }
}

public sealed record GetModerationSettingsQueryResponse
{
    public TimeSpan AutoPardonAfter { get; internal set; }
    public bool ModuleEnabled { get; internal set; }
    public ulong? PublicBanLog { get; internal set; }
}
