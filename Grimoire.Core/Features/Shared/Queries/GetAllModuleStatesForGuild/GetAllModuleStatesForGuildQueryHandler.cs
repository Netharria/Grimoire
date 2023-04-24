// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Shared.Queries.GetAllModuleStatesForGuild
{
    public class GetAllModuleStatesForGuildQueryHandler : IRequestHandler<GetAllModuleStatesForGuildQuery, GetAllModuleStatesForGuildQueryResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetAllModuleStatesForGuildQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetAllModuleStatesForGuildQueryResponse> Handle(GetAllModuleStatesForGuildQuery request, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Guilds
                .WhereIdIs(request.GuildId)
                .Select(x => new GetAllModuleStatesForGuildQueryResponse
                {
                    LevelingIsEnabled = x.LevelSettings.ModuleEnabled,
                    UserLogIsEnabled = x.UserLogSettings.ModuleEnabled,
                    ModerationIsEnabled = x.ModerationSettings.ModuleEnabled,
                    MessageLogIsEnabled = x.MessageLogSettings.ModuleEnabled,
                }).FirstAsync(cancellationToken: cancellationToken);
    }
}
