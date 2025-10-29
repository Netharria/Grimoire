// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    private static Module MapToModule(ModuleArguments moduleArgument)
    {
        return moduleArgument switch
        {
            ModuleArguments.Leveling => Module.Leveling,
            ModuleArguments.UserLog => Module.UserLog,
            ModuleArguments.Moderation => Module.Moderation,
            ModuleArguments.MessageLog => Module.MessageLog,
            ModuleArguments.Commands => Module.Commands,
            _ => throw new UnreachableException("Invalid module argument.")
        };
    }

    [UsedImplicitly]
    [Command("Set")]
    [Description("Enable or Disable a module.")]
    public async Task SetAsync(CommandContext ctx,
        [Parameter("Module")] [Description("The module to enable or disable.")]
        ModuleArguments module,
        [Parameter("Enable")] [Description("Whether to enable or disable the module.")]
        bool enable)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await this._settingsModule.SetModuleState(MapToModule(module), guild.Id, enable);

        await ctx.EditReplyAsync(message: $"{(enable ? "Enabled" : "Disabled")} {module}");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Description = $"{ctx.User.Username} {(enable ? "Enabled" : "Disabled")} {module}",
            Color = GrimoireColor.Purple
        });
    }

    internal enum ModuleArguments
    {
        Leveling,
        UserLog,
        Moderation,
        MessageLog,
        Commands
    }
}
