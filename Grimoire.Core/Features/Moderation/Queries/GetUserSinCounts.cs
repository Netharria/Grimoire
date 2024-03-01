// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Moderation.Queries;
public static class GetUserSinCounts
{
    public sealed record Query : IQuery<Response?>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext dbContext) : IQueryHandler<Query, Response?>
    {
        private readonly GrimoireDbContext _dbContext = dbContext;

        public async ValueTask<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            var result = await this._dbContext.Members
                .AsNoTracking()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(x => new
                {
                    x.Guild.ModerationSettings.ModuleEnabled,
                    Response = new Response
                    {
                        WarnCount = x.UserSins
                        .Where(x => x.SinOn > DateTimeOffset.UtcNow - x.Guild.ModerationSettings.AutoPardonAfter)
                        .Count(x => x.SinType == SinType.Warn),
                        MuteCount = x.UserSins
                        .Where(x => x.SinOn > DateTimeOffset.UtcNow - x.Guild.ModerationSettings.AutoPardonAfter)
                        .Count(x => x.SinType == SinType.Mute),
                        BanCount = x.UserSins
                        .Where(x => x.SinOn > DateTimeOffset.UtcNow - x.Guild.ModerationSettings.AutoPardonAfter)
                        .Count(x => x.SinType == SinType.Ban),
                    }
                }).FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find that user. Have they been on the server before?");
            if (!result.ModuleEnabled)
                return null;
            return result.Response;
        }
    }

    public sealed record Response
    {
        public required int WarnCount { get; init; }
        public required int MuteCount { get; init; }
        public required int BanCount { get; init; }
    }
}
