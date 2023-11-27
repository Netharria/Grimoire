// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Moderation.Queries;

public sealed record GetBanQuery : IRequest<GetBanQueryResponse>
{
    public long SinId { get; init; }
    public ulong GuildId { get; init; }
}


public class GetBanQueryHandler(IGrimoireDbContext grimoireDbContext) : IRequestHandler<GetBanQuery, GetBanQueryResponse>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetBanQueryResponse> Handle(GetBanQuery request, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Sins
            .AsNoTracking()
            .Where(x => x.SinType == SinType.Ban)
            .Where(x => x.Id == request.SinId)
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new
            {
                x.UserId,
                UsernameHistory = x.Member.User.UsernameHistories.OrderByDescending(x => x.Timestamp).First(),
                x.Guild.ModerationSettings.PublicBanLog,
                x.SinOn,
                x.Guild.ModChannelLog,
                x.Reason,
                PublishedBan = x.PublishMessages.Where(x => x.PublishType  == PublishType.Ban).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (result is null)
            throw new AnticipatedException("Could not find a ban with that Sin Id");
        if (result.PublicBanLog is null)
            throw new AnticipatedException("No Public Ban Log is configured.");

        return new GetBanQueryResponse
        {
            UserId = result.UserId,
            Username = result.UsernameHistory.Username,
            BanLogId = result.PublicBanLog.Value,
            Date = result.SinOn,
            LogChannelId = result.ModChannelLog,
            Reason = result.Reason,
            PublishedMessage = result.PublishedBan?.MessageId
        };
    }
}

public sealed record GetBanQueryResponse : BaseResponse
{
    public ulong BanLogId { get; init; }
    public DateTimeOffset Date { get; init; }
    public string Username { get; init; } = string.Empty;
    public ulong UserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public ulong? PublishedMessage { get; init; }
}
