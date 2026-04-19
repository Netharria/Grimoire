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

        if (ctx.Guild is not { } guild)
        {
            await ctx.ReplyAsync(message: "You need to be in a guild to use this command.");
            return;
        }

        var response = await this._settingsModule.GetLevelingSettings(guild.GetGuildId());
        var moduleEnabled = await this._settingsModule.IsModuleEnabled(Module.Leveling, guild.GetGuildId());
        var levelChannelLog =
            await this._settingsModule.GetEffectiveLogChannelSetting(GuildLogType.Leveling, guild.GetGuildId());

        var levelLogMention =
            levelChannelLog is null
                ? "None"
                : ctx.Guild.Channels.GetValueOrDefault(levelChannelLog.Value.Value)?.Mention;
        await ctx.ReplyAsync(
            title: "Current Level System Settings",
            message: $"**Module Enabled:** {moduleEnabled}\n" +
                     $"**Text Time:** {response.XpTimeoutPeriod.Value.TotalMinutes} minutes.\n" +
                     $"**Base:** {response.Base}\n" +
                     $"**Modifier:** {response.Modifier}\n" +
                     $"**Reward Amount:** {response.Amount}\n" +
                     $"**Log-Channel:** {levelLogMention}\n");
    }
}
