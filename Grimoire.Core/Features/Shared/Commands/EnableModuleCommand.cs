// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Core.Enums;

namespace Grimoire.Core.Features.Shared.Commands;

public sealed record EnableModuleCommand : ICommand<EnableModuleCommandResponse>
{
    public ulong GuildId { get; init; }
    public Module Module { get; init; }
    public bool Enable { get; init; }
}

public sealed class EnableModuleCommandHandler(GrimoireDbContext grimoireDbContext) : ICommandHandler<EnableModuleCommand, EnableModuleCommandResponse>
{
    private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<EnableModuleCommandResponse> Handle(EnableModuleCommand command, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Guilds
            .WhereIdIs(command.GuildId)
            .GetModulesOfType(command.Module)
            .Select(x => new
            {
                Module = x,
                x.Guild.ModChannelLog,
            })
            .FirstAsync(cancellationToken);
        var ModChannelLog = result.ModChannelLog;
        var guildModule = result.Module ?? command.Module switch
        {
            Module.Leveling => new GuildLevelSettings { GuildId = command.GuildId },
            Module.UserLog => new GuildUserLogSettings { GuildId = command.GuildId },
            Module.Moderation => new GuildModerationSettings { GuildId = command.GuildId },
            Module.MessageLog => new GuildMessageLogSettings { GuildId = command.GuildId },
            Module.Commands => new GuildCommandsSettings { GuildId = command.GuildId },
            _ => throw new NotImplementedException(),
        };
        guildModule.ModuleEnabled = command.Enable;
        if (result.Module is null)
            await this._grimoireDbContext.AddAsync(guildModule, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return new EnableModuleCommandResponse
        {
            ModerationLog = ModChannelLog
        };
    }
}

public sealed record EnableModuleCommandResponse : BaseResponse
{
    public ulong? ModerationLog { get; init; }
}
