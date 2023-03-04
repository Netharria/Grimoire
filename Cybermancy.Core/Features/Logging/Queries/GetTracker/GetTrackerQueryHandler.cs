// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Core.Extensions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.GetTracker
{
    public class GetTrackerQueryHandler : IRequestHandler<GetTrackerQuery, GetTrackerQueryResponse?>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetTrackerQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetTrackerQueryResponse?> Handle(GetTrackerQuery request, CancellationToken cancellationToken)
            =>  await this._cybermancyDbContext.Trackers
            .WhereMemberIs(request.UserId, request.GuildId)
            .Select(x => new GetTrackerQueryResponse
            {
                TrackerChannelId = x.LogChannelId
            }).FirstOrDefaultAsync(cancellationToken);

    }
}
