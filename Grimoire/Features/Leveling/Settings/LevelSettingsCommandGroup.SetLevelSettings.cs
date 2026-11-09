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
using Grimoire.Features.Shared.Commands;
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
            _ => throw new InvariantViolationException("Invalid setting.")
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

        var guild = ctx.RequiredGuild;
        await ctx.ReplyResultAsync(
            await this._settingsModule.SetLevelingSettings(guild.GetGuildId(), ctx.GetModeratorId(),
                ToLevelSettings(levelSettingsOptions), value),
            $"Updated {levelSettingsOptions} level setting to {value}",
            () => this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.DarkPurple,
                Description = $"{ctx.User.Mention} updated {levelSettingsOptions} level setting to {value}"
            }));
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
        await ChannelSettingCommand.SetAsync(ctx, this._guildLog, option, channel, "the level log",
            channelId => this._settingsModule.SetLogChannelSetting(GuildLogType.Leveling, ctx.RequiredGuild.GetGuildId(),
                ctx.GetModeratorId(), channelId),
            color: GrimoireColor.DarkPurple);
    }
}
