// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Queries.GetUserMute
{
    public class GetUserMuteQueryHandler : IQueryHandler<GetUserMuteQuery, ulong?>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetUserMuteQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<ulong?> Handle(GetUserMuteQuery query, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Mutes
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Where(x => x.Guild.ModerationSettings.ModuleEnabled)
                .Select(x => 
                    x.Guild.ModerationSettings.MuteRole)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
