// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Leveling.Queries.GetLevelSettings
{
    public class GetLevelSettingsHandler : IRequestHandler<GetLevelSettingsQuery, GetLevelSettingsResponse>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public GetLevelSettingsHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetLevelSettingsResponse> Handle(GetLevelSettingsQuery request, CancellationToken cancellationToken)
        {
            var guildLevelSettings = await this._cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new
                {
                    x.IsLevelingEnabled,
                    x.TextTime,
                    x.Base,
                    x.Modifier,
                    x.Amount,
                    x.LevelChannelLogId
                }).FirstAsync(cancellationToken: cancellationToken);
            return new GetLevelSettingsResponse
            {
                Success = true,
                IsLevelingEnabled = guildLevelSettings.IsLevelingEnabled,
                TextTime = guildLevelSettings.TextTime,
                Base = guildLevelSettings.Base,
                Modifier = guildLevelSettings.Modifier,
                Amount = guildLevelSettings.Amount,
                LevelChannelLog = guildLevelSettings.LevelChannelLogId
            };
        }
            
    }
}
