// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Queries.GetModActionsCounts
{
    public class GetModActionCountsQueryHandler : IQueryHandler<GetModActionCountsQuery, GetModActionCountsQueryResponse?>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetModActionCountsQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetModActionCountsQueryResponse?> Handle(GetModActionCountsQuery query, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Members
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(x => new GetModActionCountsQueryResponse
                {
                    BanCount = x.ModeratedSins.Count(x => x.SinType == SinType.Ban),
                    MuteCount = x.ModeratedSins.Count(x => x.SinType == SinType.Mute),
                    WarnCount = x.ModeratedSins.Count(x => x.SinType == SinType.Warn)
                }).FirstOrDefaultAsync(cancellationToken);
    }
}
