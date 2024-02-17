// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Leveling.Queries;

public sealed class GetLevelSettings
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            var guildLevelSettings = await this._grimoireDbContext.GuildLevelSettings
            .Where(x => x.GuildId == request.GuildId)
            .Select(x => new
            {
                x.ModuleEnabled,
                x.TextTime,
                x.Base,
                x.Modifier,
                x.Amount,
                x.LevelChannelLogId
            }).FirstAsync(cancellationToken: cancellationToken);
            return new Response
            {
                ModuleEnabled = guildLevelSettings.ModuleEnabled,
                TextTime = guildLevelSettings.TextTime,
                Base = guildLevelSettings.Base,
                Modifier = guildLevelSettings.Modifier,
                Amount = guildLevelSettings.Amount,
                LevelChannelLog = guildLevelSettings.LevelChannelLogId
            };
        }

    }

    public sealed record Response : BaseResponse
    {
        public bool ModuleEnabled { get; init; }
        public TimeSpan TextTime { get; init; }
        public int Base { get; init; }
        public int Modifier { get; init; }
        public int Amount { get; init; }
        public ulong? LevelChannelLog { get; init; }
    }


}

