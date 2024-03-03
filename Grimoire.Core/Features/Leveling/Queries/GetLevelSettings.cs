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
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

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
            }).FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (guildLevelSettings is null)
                throw new AnticipatedException("Could not find that level settings for that server.");

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
        public required bool ModuleEnabled { get; init; }
        public required TimeSpan TextTime { get; init; }
        public required int Base { get; init; }
        public required int Modifier { get; init; }
        public required int Amount { get; init; }
        public ulong? LevelChannelLog { get; init; }
    }


}

