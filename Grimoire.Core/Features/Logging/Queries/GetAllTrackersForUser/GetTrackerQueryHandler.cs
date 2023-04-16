// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Logging.Queries.GetAllTrackersForUser
{
    public class GetAllTrackersForUserQueryHandler : IRequestHandler<GetAllTrackersForUserQuery, GetAllTrackersForUserQueryResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetAllTrackersForUserQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetAllTrackersForUserQueryResponse> Handle(GetAllTrackersForUserQuery request, CancellationToken cancellationToken)
            => new GetAllTrackersForUserQueryResponse
            {
                Trackers = await this._grimoireDbContext.Trackers
                    .Where(x => x.UserId == request.UserId)
                    .Select(x => new UserTracker
                    {

                        TrackerChannelId = x.LogChannelId,
                        GuildId = x.GuildId,
                    }).ToArrayAsync(cancellationToken)
            };
    }
}
