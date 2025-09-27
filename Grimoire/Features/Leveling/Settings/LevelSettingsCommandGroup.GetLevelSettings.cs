// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{
    [Command("View")]
    [Description("View the current settings for the leveling module.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var moduleState = await this._settingsModule.IsModuleEnabled(Module.Leveling, ctx.Guild.Id);

        var levelLogId = await this._settingsModule.GetLogChannelSetting(GuildLogType.Leveling, ctx.Guild.Id);

        var levelingSettings = await this._settingsModule.GetLevelingSettings(ctx.Guild.Id);

        var levelLogMention =
            levelLogId is null
                ? "None"
                : ctx.Guild.Channels.GetValueOrDefault(levelLogId.Value)?.Mention;
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
