// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Leveling.Queries.GetLevelSettings
{
    public class GetLevelSettingsQueryHandler : IRequestHandler<GetLevelSettingsQuery, GetLevelSettingsQueryResponse>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public GetLevelSettingsQueryHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetLevelSettingsQueryResponse> Handle(GetLevelSettingsQuery request, CancellationToken cancellationToken)
        {
            var guildLevelSettings = await this._cybermancyDbContext.GuildLevelSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new
                {
                    x.ModuleEnabled,
                    x.TextTime,
                    x.Base,
                    x.Modifier,
                    x.Amount,
                    x.LevelChannelLogId
                }).FirstAsync(cancellationToken: cancellationToken);
            return new GetLevelSettingsQueryResponse
            {
                ModuleEnabled = guildLevelSettings.ModuleEnabled,
                TextTime = guildLevelSettings.TextTime,
                Base = guildLevelSettings.Base,
                Modifier = guildLevelSettings.Modifier,
                Amount = guildLevelSettings.Amount,
                LevelChannelLog = guildLevelSettings.LevelChannelLogId
            };
        }
            
    }
}
