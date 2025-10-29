// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("View")]
    [Description("View the current moderation settings for this server.")]
    public async Task ViewSettingsAsync(CommandContext ctx)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var autoPardonAfter = await this._settingsModule.GetAutoPardonDuration(guild.Id);
        var banLogChannel = await this._settingsModule.GetLogChannelSetting(GuildLogType.BanLog, guild.Id);
        var moduleEnabled = await this._settingsModule.IsModuleEnabled(Module.Moderation, guild.Id);

        var banLog = banLogChannel is null
            ? "None"
            : ChannelExtensions.Mention(banLogChannel.Value);

        var autoPardonString =
            autoPardonAfter.Days % 365 == 0
                ? $"{autoPardonAfter.Days / 365} years"
                : autoPardonAfter.Days % 30 == 0
                    ? $"{autoPardonAfter.Days / 30} months"
                    : $"{autoPardonAfter.Days} days";

        await ctx.EditReplyAsync(
            title: "Current moderation System Settings",
            message: $"**Module Enabled:** {moduleEnabled}\n" +
                     $"**Auto Pardon Duration:** {autoPardonString}\n" +
                     $"**Ban Log:** {banLog}\n");
    }
}
