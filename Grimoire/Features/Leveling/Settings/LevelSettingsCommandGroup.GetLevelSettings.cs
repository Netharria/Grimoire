// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Enums;
using LanguageExt;
using static LanguageExt.Prelude;

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
        var guild = ctx.Guild!;

        var result =
                from _ in liftIO(() => ctx.DeferResponseAsync().AsTask())
                from ModuleState in liftIO(() =>
                    this._settingsModule.IsModuleEnabled(Module.Leveling, guild.GetGuildId()))
                from levelLogId in liftIO(() =>
                    this._settingsModule.GetLogChannelSetting(GuildLogType.Leveling, guild.GetGuildId()))
                from LevelingSettingEntry in this._settingsModule.GetLevelingSettings(guild.GetGuildId())
                from LevelingChannel in liftIO(() => ctx.Client.GetChannelOrDefaultAsync(levelLogId))
                    .Map(channel => channel is null
                        ? "None"
                        : channel.Mention)
                select new { LevelingSettingEntry, LevelingChannel, ModuleState };

    await result
            .Run()
            .Match(
                Succ: resultItems =>
                ctx.EditReplyAsync(
                        title: "Current Level System Settings",
                        message: $"""
                                   **Module Enabled:** {resultItems.ModuleState}
                                   **Text Time:** {resultItems.LevelingSettingEntry.TextTime.TotalMinutes} minutes.
                                   **Base:** {resultItems.LevelingSettingEntry.Base}
                                   **Modifier:** {resultItems.LevelingSettingEntry.Modifier}
                                   **Reward Amount:** {resultItems.LevelingSettingEntry.Amount}
                                   **Log-Channel:** {resultItems.LevelingChannel}
                                   """),
                Fail: error => ctx.SendErrorResponseAsync(error.Message));
    }
}
