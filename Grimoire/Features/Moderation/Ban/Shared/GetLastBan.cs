// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Ban.Shared;

public sealed class GetLastBan
{
    public sealed record Query : IRequest<Response?>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(request.UserId, request.GuildId)
                .Select(member => new Response
                {
                    LastSin = member.UserSins.OrderByDescending(x => x.SinOn)
                        .Where(sin => sin.SinType == SinType.Ban)
                        .Select(sin => new LastSin
                        {
                            SinId = sin.Id, ModeratorId = sin.ModeratorId, Reason = sin.Reason, SinOn = sin.SinOn
                        })
                        .FirstOrDefault(),
                    LogChannelId = member.Guild.ModChannelLog,
                    ModerationModuleEnabled = member.Guild.ModerationSettings.ModuleEnabled
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public bool ModerationModuleEnabled { get; init; }

        public LastSin? LastSin { get; init; }
    }

    public sealed record LastSin
    {
        public long SinId { get; init; }
        public ulong? ModeratorId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DateTimeOffset SinOn { get; init; }
    }
}
