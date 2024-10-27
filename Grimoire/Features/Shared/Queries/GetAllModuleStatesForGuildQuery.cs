// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Shared.Queries;

public sealed record GetAllModuleStatesForGuildQuery : IRequest<GetAllModuleStatesForGuildQueryResponse?>
{
    public ulong GuildId { get; init; }
}

public sealed class GetAllModuleStatesForGuildQueryHandler(GrimoireDbContext grimoireDbContext) : IRequestHandler<GetAllModuleStatesForGuildQuery, GetAllModuleStatesForGuildQueryResponse?>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public Task<GetAllModuleStatesForGuildQueryResponse?> Handle(GetAllModuleStatesForGuildQuery request, CancellationToken cancellationToken)
        => this._grimoireDbContext.Guilds
            .AsNoTracking()
            .WhereIdIs(request.GuildId)
            .Select(x => new GetAllModuleStatesForGuildQueryResponse
            {
                // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                LevelingIsEnabled = x.LevelSettings != null && x.LevelSettings.ModuleEnabled,
                UserLogIsEnabled = x.UserLogSettings != null && x.UserLogSettings.ModuleEnabled,
                ModerationIsEnabled = x.ModerationSettings != null && x.ModerationSettings.ModuleEnabled,
                MessageLogIsEnabled = x.MessageLogSettings != null && x.MessageLogSettings.ModuleEnabled,
                CommandsIsEnabled = x.CommandsSettings != null && x.CommandsSettings.ModuleEnabled
                // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            }).FirstOrDefaultAsync(cancellationToken: cancellationToken);
}

public sealed record GetAllModuleStatesForGuildQueryResponse : BaseResponse
{
    public bool LevelingIsEnabled { get; init; }
    public bool UserLogIsEnabled { get; init; }
    public bool ModerationIsEnabled { get; init; }
    public bool MessageLogIsEnabled { get; init; }
    public bool CommandsIsEnabled { get; init; }
}
