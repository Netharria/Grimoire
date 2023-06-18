// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetUserLogSettings;

public class GetUserLogSettingsQueryHandler : IRequestHandler<GetUserLogSettingsQuery, GetUserLogSettingsQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext;

    public GetUserLogSettingsQueryHandler(GrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<GetUserLogSettingsQueryResponse> Handle(GetUserLogSettingsQuery request, CancellationToken cancellationToken)
    {
        return await this._grimoireDbContext.GuildUserLogSettings
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new GetUserLogSettingsQueryResponse
            {
                JoinChannelLog = x.JoinChannelLogId,
                LeaveChannelLog = x.LeaveChannelLogId,
                UsernameChannelLog = x.UsernameChannelLogId,
                NicknameChannelLog = x.NicknameChannelLogId,
                AvatarChannelLog = x.AvatarChannelLogId,
                IsLoggingEnabled = x.ModuleEnabled
            }).FirstAsync(cancellationToken: cancellationToken);
    }
}
