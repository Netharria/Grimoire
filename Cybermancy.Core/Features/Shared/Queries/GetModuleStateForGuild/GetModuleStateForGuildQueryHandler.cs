// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Shared.Queries.GetModuleStateForGuild
{
    public class GetModuleStateForGuildQueryHandler : IRequestHandler<GetModuleStateForGuildQuery, bool>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetModuleStateForGuildQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<bool> Handle(GetModuleStateForGuildQuery request, CancellationToken cancellationToken)
        {
            var query = this._cybermancyDbContext.Guilds.AsNoTracking().Where(x => x.Id == request.GuildId);
            return request.Module switch
            {
                Module.Leveling => await query.Select(x => x.LevelSettings.IsLevelingEnabled).FirstAsync(cancellationToken: cancellationToken),
                Module.Logging => await query.Select(x => x.LogSettings.IsLoggingEnabled).FirstAsync(cancellationToken: cancellationToken),
                Module.Moderation => await query.Select(x => x.ModerationSettings.IsModerationEnabled).FirstAsync(cancellationToken: cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(request), request, message: null)
            };
        }
    }
}
