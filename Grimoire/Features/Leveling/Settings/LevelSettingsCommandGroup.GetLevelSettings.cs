// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

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

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        var levelLogMention =
            guildSettings.LevelSettings.LevelChannelLogId is null
                ? "None"
                : ctx.Guild.Channels.GetValueOrDefault(guildSettings.LevelSettings.LevelChannelLogId.Value)?.Mention;
        await ctx.EditReplyAsync(
            title: "Current Level System Settings",
            message: $"**Module Enabled:** {guildSettings.LevelSettings.ModuleEnabled}\n" +
                     $"**Text Time:** {guildSettings.LevelSettings.TextTime.TotalMinutes} minutes.\n" +
                     $"**Base:** {guildSettings.LevelSettings.Base}\n" +
                     $"**Modifier:** {guildSettings.LevelSettings.Modifier}\n" +
                     $"**Reward Amount:** {guildSettings.LevelSettings.Amount}\n" +
                     $"**Log-Channel:** {levelLogMention}\n");
    }
}
