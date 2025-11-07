// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Diagnostics;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{
    public enum LevelSettingsOptions
    {
        [ChoiceDisplayName("Timeout between xp gains in minutes")]
        TextTime,

        [ChoiceDisplayName("Base - linear xp per level modifier")]
        Base,

        [ChoiceDisplayName("Modifier - exponential xp per level modifier")]
        Modifier,

        [ChoiceDisplayName("Amount per xp gain.")]
        Amount


    }

    public static SettingsModule.LevelSettings ToLevelSettings(LevelSettingsOptions levelSettingsOptions)
        => levelSettingsOptions switch
        {
            LevelSettingsOptions.Amount => SettingsModule.LevelSettings.Amount,
            LevelSettingsOptions.Base => SettingsModule.LevelSettings.Base,
            LevelSettingsOptions.Modifier => SettingsModule.LevelSettings.Modifier,
            LevelSettingsOptions.TextTime => SettingsModule.LevelSettings.TextTime,
            _ => throw new UnreachableException("Invalid setting.")
        };

    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Set")]
    [Description("Set a leveling setting.")]
    public async Task SetAsync(
        CommandContext ctx,
        [Parameter("Setting")] [Description("The setting to change.")]
        LevelSettingsOptions levelSettingsOptions,
        [MinMaxValue(1, int.MaxValue)] [Parameter("Value")] [Description("The value to change the setting to.")]
        int value)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await this._settingsModule.SetLevelingSettings(ToLevelSettings(levelSettingsOptions), value, guild.GetGuildId())
            .Run()
            .Match(
                _ => HandleSettingSuccess(ctx, guild, levelSettingsOptions, value, this._guildLog),
                error => ctx.SendErrorResponseAsync(error.Message));
    }

    private static async Task HandleSettingSuccess(
        CommandContext ctx,
        DiscordGuild guild,
        LevelSettingsOptions levelSettingsOptions,
        int value,
        GuildLog guildLog)
    {
        await ctx.EditReplyAsync(message: $"Updated {levelSettingsOptions} level setting to {value}");
        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = $"{ctx.User.Mention} updated {levelSettingsOptions} level setting to {value}"
        });
    }


    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("LogSet")]
    [Description("Set the leveling log channel.")]
    public async Task LogSetAsync(
        CommandContext ctx,
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel")]
        ChannelOption option,
        [Parameter("Channel")] [Description("The channel to change the log to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await ctx.GetChannelOption(option, channel)
            .Bind(ch => ValidateChannelPermissions(guild, ch))
            .Match(ch => HandleSuccess(
                    ctx, guild, option, ch, this._settingsModule, this._guildLog),
                error => ctx.SendErrorResponseAsync(error.Message));
    }

    private static  Fin<DiscordChannel?> ValidateChannelPermissions(
        DiscordGuild guild,
        DiscordChannel? channel)
    {
        if (channel is null)
            return (DiscordChannel?) null;

        var permissions = channel.PermissionsFor(guild.CurrentMember);

        return permissions.HasPermission(DiscordPermission.SendMessages)
            ? channel
            : Error.New($"{guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
    }

    private static async Task HandleSuccess(
        CommandContext ctx,
        DiscordGuild guild,
        ChannelOption option,
        DiscordChannel? channel,
        SettingsModule settingsModule,
        GuildLog guildLog)
    {
        await settingsModule.SetLogChannelSetting(GuildLogType.Leveling, guild.GetGuildId(), channel?.GetChannelId());

        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? "Disabled the level log."
            : $"Updated the level log to {channel?.Mention}");

        await guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the level log."
                : $"{ctx.User.Mention} updated the level log to {channel?.Mention}."
        });
    }
}
