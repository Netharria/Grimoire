// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Queries.GetBan;

namespace Grimoire.Core.Features.Moderation.Queries.GetUnban
{
    public class GetUnbanQueryHandler : IRequestHandler<GetUnbanQuery, GetBanQueryResponse>
    {
        private readonly IGrimoireDbContext _grimoireDbContext;

        public GetUnbanQueryHandler(IGrimoireDbContext grimoireDbContext)
        {
            this._grimoireDbContext = grimoireDbContext;
        }

        public async ValueTask<GetBanQueryResponse> Handle(GetUnbanQuery request, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Sins
                .Where(x => x.SinType == SinType.Ban)
                .Where(x => x.Id == request.SinId)
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new
                {
                    x.UserId,
                    UsernameHistory = x.Member.User.UsernameHistories.OrderByDescending(x => x.Timestamp).First(),
                    x.Guild.ModerationSettings.PublicBanLog,
                    x.Guild.ModChannelLog,
                    x.Pardon,
                    PublishedUnban = x.PublishMessages.Where(x => x.PublishType  == PublishType.Unban).FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (result is null)
                throw new AnticipatedException("Could not find a ban with that Sin Id");
            if (result.PublicBanLog is null)
                throw new AnticipatedException("No Public Ban Log is configured.");
            if (result.Pardon is null)
                throw new AnticipatedException("The ban must be pardoned first before the unban can be published.");

            return new GetBanQueryResponse
            {
                UserId = result.UserId,
                Username = result.UsernameHistory.Username,
                BanLogId = result.PublicBanLog.Value,
                Date = result.Pardon.PardonDate,
                LogChannelId = result.ModChannelLog,
                Reason = result.Pardon.Reason,
                PublishedMessage = result.PublishedUnban?.MessageId
            };
        }
    }
}