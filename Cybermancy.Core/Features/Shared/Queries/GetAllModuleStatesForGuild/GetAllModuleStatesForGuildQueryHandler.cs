// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.DatabaseQueryHelpers;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Queries.GetAllModuleStatesForGuild
{
    public class GetAllModuleStatesForGuildQueryHandler : IRequestHandler<GetAllModuleStatesForGuildQuery, GetAllModuleStatesForGuildQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetAllModuleStatesForGuildQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetAllModuleStatesForGuildQueryResponse> Handle(GetAllModuleStatesForGuildQuery request, CancellationToken cancellationToken)
            => await this._cybermancyDbContext.Guilds
                .WhereIdIs(request.GuildId)
                .Select(x => new GetAllModuleStatesForGuildQueryResponse
                {
                    LevelingIsEnabled = x.LevelSettings.ModuleEnabled,
                    LoggingIsEnabled = x.LogSettings.ModuleEnabled,
                    ModerationIsEnabled = x.ModerationSettings.ModuleEnabled,
                }).FirstAsync(cancellationToken: cancellationToken);
    }
}
