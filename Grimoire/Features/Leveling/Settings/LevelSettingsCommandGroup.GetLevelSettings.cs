// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("View")]
    [Description("View the current settings for the leveling module.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var moduleState = await this._settingsModule.IsModuleEnabled(Module.Leveling, guild.Id);

        var levelLogId = await this._settingsModule.GetLogChannelSetting(GuildLogType.Leveling, guild.Id);

        var levelingSettings = await this._settingsModule.GetLevelingSettings(guild.Id);

        var levelingChannel = await ctx.Client.GetChannelOrDefaultAsync(levelLogId);

        var levelLogMention =
            levelingChannel is null
                ? "None"
                : levelingChannel.Mention;
        await ctx.EditReplyAsync(
            title: "Current Level System Settings",
            message: $"**Module Enabled:** {moduleState}\n" +
                     $"**Text Time:** {levelingSettings.TextTime.TotalMinutes} minutes.\n" +
                     $"**Base:** {levelingSettings.Base}\n" +
                     $"**Modifier:** {levelingSettings.Modifier}\n" +
                     $"**Reward Amount:** {levelingSettings.Amount}\n" +
                     $"**Log-Channel:** {levelLogMention}\n");
    }
}
