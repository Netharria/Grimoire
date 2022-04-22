// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.GetAllTrackersForUser
{
    public class GetAllTrackersForUserQueryHandler : IRequestHandler<GetAllTrackersForUserQuery, GetAllTrackersForUserQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetAllTrackersForUserQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async Task<GetAllTrackersForUserQueryResponse> Handle(GetAllTrackersForUserQuery request, CancellationToken cancellationToken)
            => new GetAllTrackersForUserQueryResponse
            {
                Success = true,
                Trackers = await _cybermancyDbContext.Trackers
                    .Where(x => x.UserId == request.UserId)
                    .Select(x => new UserTracker
                    {

                        TrackerChannelId = x.LogChannelId,
                        GuildId = x.GuildId,
                    }).ToArrayAsync(cancellationToken)
            };
    }
}
