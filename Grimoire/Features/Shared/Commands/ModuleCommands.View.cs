﻿// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    [UsedImplicitly]
    [Command("View")]
    [Description("View the current states of the modules.")]
    public async Task ViewAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var guildSettings = await this._settingsModule.GetAllModuleState(ctx.Guild.Id);

        await ctx.EditReplyAsync(
            title: "Current states of modules.",
            message: $"**Leveling Enabled:** {guildSettings.LevelingEnabled}\n" +
                     $"**User Log Enabled:** {guildSettings.UserLogEnabled}\n" +
                     $"**Message Log Enabled:** {guildSettings.MessageLogEnabled}\n" +
                     $"**Moderation Enabled:** {guildSettings.ModerationEnabled}\n" +
                     $"**Commands Enabled:** {guildSettings.CommandsEnabled}\n");
    }
}
