// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Queries;

public sealed record GetLastBanQuery : IRequest<GetLastBanQueryResponse>
{
    public ulong UserId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class GetLastBanQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetLastBanQuery, GetLastBanQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async Task<GetLastBanQueryResponse> Handle(GetLastBanQuery request, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Members
            .AsNoTracking()
            .WhereMemberHasId(request.UserId, request.GuildId)
            .Select(member => new GetLastBanQueryResponse
            {
                UserId = member.UserId,
                GuildId = member.GuildId,
                LastSin = member.UserSins.OrderByDescending(x => x.SinOn)
                        .Where(sin => sin.SinType == SinType.Ban)
                        .Select(sin => new LastSin
                        {
                            SinId = sin.Id,
                            ModeratorId = sin.ModeratorId,
                            Reason = sin.Reason,
                            SinOn = sin.SinOn
                        })
                    .FirstOrDefault(),

                LogChannelId = member.Guild.ModChannelLog,
                ModerationModuleEnabled = member.Guild.ModerationSettings.ModuleEnabled
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (result is null)
            return new GetLastBanQueryResponse();

        return result;
    }
}

public sealed record GetLastBanQueryResponse : BaseResponse
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public bool ModerationModuleEnabled { get; set; }

    public LastSin? LastSin { get; set; }
}

public sealed record LastSin
{
    public long SinId { get; set; }
    public ulong? ModeratorId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset SinOn { get; set; }
}
