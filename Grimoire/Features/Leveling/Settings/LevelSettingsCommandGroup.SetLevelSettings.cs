// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class LevelSettingsCommandGroup
{
    public enum LevelSettings
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

    [Command("Set")]
    [Description("Set a leveling setting.")]
    public async Task SetAsync(
        CommandContext ctx,
        [Parameter("Setting")] [Description("The setting to change.")]
        LevelSettings levelSettings,
        [MinMaxValue(1, int.MaxValue)] [Parameter("Value")] [Description("The value to change the setting to.")]
        int value)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var levelingSettingEntry = await this._settingsModule.GetLevelingSettings(ctx.Guild.Id);

        var result = levelSettings switch
        {
            LevelSettings.TextTime => levelingSettingEntry with { TextTime = TimeSpan.FromMinutes(value) },
            LevelSettings.Amount => levelingSettingEntry with { Amount = value },
            LevelSettings.Base => levelingSettingEntry with { Base = value },
            LevelSettings.Modifier => levelingSettingEntry with { Modifier = value },
            _ => throw new AnticipatedException("Invalid setting.")
        };

        await this._settingsModule.SetLevelingSettings(ctx.Guild.Id, result);

        await ctx.EditReplyAsync(message: $"Updated {levelSettings} level setting to {value}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = $"{ctx.User.Mention} updated {levelSettings} level setting to {value}"
        });
    }

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

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
                throw new AnticipatedException(
                    $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }

        await this._settingsModule.SetLogChannelSetting(GuildLogType.Leveling, ctx.Guild.Id, channel?.Id);

        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? "Disabled the level log."
            : $"Updated the level log to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the level log."
                : $"{ctx.User.Mention} updated the level log to {channel?.Mention}."
        });
    }
}
