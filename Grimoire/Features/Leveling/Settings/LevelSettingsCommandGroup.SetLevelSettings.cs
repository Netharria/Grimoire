// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Leveling.Settings;

[UsedImplicitly]
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

    public static LevelSettings ToLevelSettings(LevelSettingsOptions levelSettingsOptions)
        => levelSettingsOptions switch
        {
            LevelSettingsOptions.Amount => LevelSettings.Amount,
            LevelSettingsOptions.Base => LevelSettings.Base,
            LevelSettingsOptions.Modifier => LevelSettings.Modifier,
            LevelSettingsOptions.TextTime => LevelSettings.XpTimeoutPeriod,
            _ => throw new UnreachableException("Invalid setting.")
        };

    [RequireGuild]
    [RequireModuleEnabled(Module.Leveling)]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [Command("Set")]
    [Description("Set a leveling setting.")]
    public async Task SetAsync(
        CommandContext ctx,
        [Parameter("setting")] [Description("The setting to change.")]
        LevelSettingsOptions levelSettingsOptions,
        [MinMaxValue(1, int.MaxValue)] [Parameter("value")] [Description("The value to change the setting to.")]
        int value)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        await this._settingsModule
            .SetLevelingSettings(guild.GetGuildId(), ctx.GetModeratorId(), ToLevelSettings(levelSettingsOptions), value)
            .MatchAsync(
                _ => HandleSettingSuccessAsync(ctx, guild, levelSettingsOptions, value, this._guildLog),
                error => ctx.ReplyAsync(message: error.Message).AsTask());
    }

    private static async Task HandleSettingSuccessAsync(
        CommandContext ctx,
        DiscordGuild guild,
        LevelSettingsOptions levelSettingsOptions,
        int value,
        GuildLog guildLog)
    {
        await ctx.ReplyAsync(message: $"Updated {levelSettingsOptions} level setting to {value}");
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
        [Parameter("option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel")]
        ChannelOption option,
        [Parameter("channel")] [Description("The channel to change the log to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;
        channel = ctx.GetChannelOption(option, channel);

        if (ValidateChannelPermission(guild, channel) is Validation<Unit>.Invalid { Errors: var errors })
        {
            await ctx.ReplyAsync(message: string.Join("; ", errors.Select(e => e.Message)));
            return;
        }

        await this._settingsModule.SetLogChannelSetting(
            GuildLogType.Leveling,
            guild.GetGuildId(),
            ctx.GetModeratorId(),
            channel?.GetChannelId());

        var isOff = option is ChannelOption.Off;
        await ctx.ReplyAsync(message: isOff
            ? "Disabled the level log."
            : $"Updated the level log to {channel?.Mention}");

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = isOff
                ? $"{ctx.User.Mention} disabled the level log."
                : $"{ctx.User.Mention} updated the level log to {channel?.Mention}."
        });
    }

    private static Validation<Unit> ValidateChannelPermission(DiscordGuild guild, DiscordChannel? channel)
        => channel is not null && !channel.PermissionsFor(guild.CurrentMember).HasPermission(DiscordPermission.SendMessages)
            ? Validation<Unit>.Fail(new Error("log-channel.permission",
                $"{guild.CurrentMember.Mention} does not have permissions to send messages in that channel."))
            : Validation<Unit>.Succeed(Unit.Value);
}
