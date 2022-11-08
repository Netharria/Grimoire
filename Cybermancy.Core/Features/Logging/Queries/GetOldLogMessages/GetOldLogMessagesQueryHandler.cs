// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Features.Logging.Queries.GetOldLogMessages
{
    public class GetOldLogMessagesQueryHandler : IRequestHandler<GetOldLogMessagesQuery, GetOldLogMessagesQueryResponse>
    {
        private readonly CybermancyDbContext _cybermancyDbContext;

        public GetOldLogMessagesQueryHandler(CybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetOldLogMessagesQueryResponse> Handle(GetOldLogMessagesQuery request, CancellationToken cancellationToken)
        {
            var oldDate = DateTime.UtcNow - TimeSpan.FromDays(31);
            return new GetOldLogMessagesQueryResponse
            {
                Channels = await this._cybermancyDbContext.OldLogMessages
                .Where(x => x.CreatedAt == oldDate)
                .GroupBy(x => x.ChannelId)
                .Select(x => new OldLogMessageChannel
                {
                    ChannelId = x.Key,
                    MessageIds = x.Select(x => x.Id).ToArray()
                }).ToArrayAsync(cancellationToken: cancellationToken)
            };
        }
    }
}
