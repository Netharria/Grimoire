// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Logging.UserLogging.Queries;
public static class GetRecentUserAndNickNames
{
    public sealed record Query : IRequest<Response?>
    {
        public required ulong UserId { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext dbContext)
    : IRequestHandler<Query, Response?>
    {
        private readonly GrimoireDbContext _dbContext = dbContext;

        public async Task<Response?> Handle
            (Query query, CancellationToken cancellationToken)
        {
            var result = await this._dbContext.Members
                .AsNoTracking()
                .AsSplitQuery()
                .WhereMemberHasId(query.UserId, query.GuildId)
                .Select(x => new
                {
                    x.Guild.UserLogSettings.ModuleEnabled,
                    Response = new Response
                    {
                        Usernames = x.User.UsernameHistories
                        .OrderByDescending(x => x.Timestamp)
                        .Take(3)
                        .Select(x => x.Username)
                        .ToArray(),
                        Nicknames = x.NicknamesHistory
                        .Where(x => x.Nickname != null)
                        .OrderByDescending(x => x.Timestamp)
                        .Take(3)
                        .Select(x => x.Nickname)
                        .OfType<string>()
                        .ToArray(),
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
        public required string[] Usernames { get; init; }
        public required string[] Nicknames { get; init; }
    }
}
