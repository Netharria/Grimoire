// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Logging.Queries.GetTracker
{
    public class GetTrackerQueryHandler : IRequestHandler<GetTrackerQuery, GetTrackerQueryResponse?>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetTrackerQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetTrackerQueryResponse?> Handle(GetTrackerQuery request, CancellationToken cancellationToken)
            => await this._grimoireDbContext.Trackers
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Select(x => new GetTrackerQueryResponse
            {
                TrackerChannelId = x.LogChannelId
            }).FirstOrDefaultAsync(cancellationToken);

    }
}
