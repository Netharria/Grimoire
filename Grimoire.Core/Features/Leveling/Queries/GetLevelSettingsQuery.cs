// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Core.Features.Leveling.Queries;

public sealed record GetLevelSettingsQuery : IRequest<GetLevelSettingsQueryResponse>
{
    public ulong GuildId { get; init; }
}

public sealed class GetLevelSettingsQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetLevelSettingsQuery, GetLevelSettingsQueryResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<GetLevelSettingsQueryResponse> Handle(GetLevelSettingsQuery request, CancellationToken cancellationToken)
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
        return new GetLevelSettingsQueryResponse
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

public sealed record GetLevelSettingsQueryResponse : BaseResponse
{
    public bool ModuleEnabled { get; init; }
    public TimeSpan TextTime { get; init; }
    public int Base { get; init; }
    public int Modifier { get; init; }
    public int Amount { get; init; }
    public ulong? LevelChannelLog { get; init; }
}

