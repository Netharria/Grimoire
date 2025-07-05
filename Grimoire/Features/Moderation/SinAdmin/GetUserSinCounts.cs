// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.SinAdmin;

public static class GetUserSinCounts
{
    public sealed record Query : IRequest<Response?>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query query, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Members
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
                            .Count(x => x.SinType == SinType.Ban)
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
