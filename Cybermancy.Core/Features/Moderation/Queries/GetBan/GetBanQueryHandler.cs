// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Core.Features.Moderation.Queries.GetBan
{
    public class GetBanQueryHandler : IRequestHandler<GetBanQuery, GetBanQueryResponse>
    {
        private readonly ICybermancyDbContext _cybermancyDbContext;

        public GetBanQueryHandler(ICybermancyDbContext cybermancyDbContext)
        {
            this._cybermancyDbContext = cybermancyDbContext;
        }

        public async ValueTask<GetBanQueryResponse> Handle(GetBanQuery request, CancellationToken cancellationToken)
        {
            var result = await this._cybermancyDbContext.Sins
                .Where(x => x.SinType == SinType.Ban)
                .Where(x => x.Id == request.SinId)
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new 
                {
                    x.UserId,
                    UsernameHistory = x.Member.User.UsernameHistories.OrderByDescending(x => x.Timestamp).First(),
                    x.Guild.ModerationSettings.PublicBanLog,
                    x.InfractionOn,
                    x.Guild.ModChannelLog,
                    x.Reason,
                    PublishedBan = x.PublishMessages.Where(x => x.PublishType  == PublishType.Ban).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (result is null)
                throw new AnticipatedException("Could not find a ban with that Sin Id");
            if(result.PublicBanLog is null)
                throw new AnticipatedException("No Public Ban Log is configured.");

            return new GetBanQueryResponse
            {
                UserId = result.UserId,
                Username = result.UsernameHistory.Username,
                BanLogId = result.PublicBanLog.Value,
                Date = result.InfractionOn,
                LogChannelId = result.ModChannelLog,
                Reason = result.Reason,
                PublishedMessage = result.PublishedBan?.MessageId
            };
        }
    }
}
