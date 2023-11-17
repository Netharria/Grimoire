// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries.GetModerationSettings;
public class GetModerationSettingsQueryHandler(IGrimoireDbContext context) : IQueryHandler<GetModerationSettingsQuery, GetModerationSettingsQueryResponse>
{
    private readonly IGrimoireDbContext _context = context;

    public async ValueTask<GetModerationSettingsQueryResponse> Handle(GetModerationSettingsQuery query, CancellationToken cancellationToken)
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
