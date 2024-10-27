// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Shared.Queries;

public sealed class GetGeneralSettings
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(query.GuildId)
                .Select(x => new { x.ModChannelLog, x.UserCommandChannelId })
                .FirstOrDefaultAsync(cancellationToken);
            return new Response
            {
                ModLogChannel = result?.ModChannelLog, UserCommandChannel = result?.UserCommandChannelId
            };
        }
    }

    public sealed record Response
    {
        public ulong? ModLogChannel { get; init; }
        public ulong? UserCommandChannel { get; init; }
    }
}
