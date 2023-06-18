// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Shared.Queries.GetModerationLog;

public class GetModLogQueryHandler : IQueryHandler<GetModLogQuery, BaseResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public GetModLogQueryHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<BaseResponse> Handle(GetModLogQuery query, CancellationToken cancellationToken)
    {
        var modChannelLog = await this._grimoireDbContext.Guilds
            .WhereIdIs(query.GuildId)
            .Select(x => x.ModChannelLog)
            .FirstOrDefaultAsync(cancellationToken);
        return new BaseResponse
        {
            LogChannelId = modChannelLog
        };
    }
}
