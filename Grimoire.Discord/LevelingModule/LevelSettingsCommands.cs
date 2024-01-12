// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Leveling.Commands;
using Grimoire.Core.Features.Leveling.Queries;
using Grimoire.Discord.Enums;

namespace Grimoire.Discord.LevelingModule;



[SlashCommandGroup("LevelSettings", "Changes the settings of the Leveling Module.")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Leveling)]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
public class LevelSettingsCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("View", "View the current settings for the leveling module.")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new GetLevelSettingsQuery{ GuildId = ctx.Guild.Id });
        var levelLogMention =
                response.LevelChannelLog is null ?
                "None" :
                ctx.Guild.GetChannel(response.LevelChannelLog.Value).Mention;
        await ctx.EditReplyAsync(
            title: "Current Level System Settings",
            message: $"**Module Enabled:** {response.ModuleEnabled}\n" +
            $"**Texttime:** {response.TextTime.TotalMinutes} minutes.\n" +
            $"**Base:** {response.Base}\n" +
            $"**Modifier:** {response.Modifier}\n" +
            $"**Reward Amount:** {response.Amount}\n" +
            $"**Log-Channel:** {levelLogMention}\n");
    }

    [SlashCommand("Set", "Set a leveling setting.")]
    public async Task SetAsync(
        InteractionContext ctx,
        [Choice("Timeout between xp gains in minutes", 0)]
        [Choice("Base - linear xp per level modifier", 1)]
        [Choice("Modifier - exponential xp per level modifier", 2)]
        [Choice("Amount per xp gain.", 3)]
        [Option("Setting", "The Setting to change.")] long levelSettings,
        [Maximum(int.MaxValue)]
        [Minimum(1)]
        [Option("Value", "The value to change the setting to.")] long value)
    {
        await ctx.DeferAsync();
        var levelSetting = (LevelSettings)levelSettings;
        var response = await this._mediator.Send(new SetLevelSettingsCommand
        {
            GuildId = ctx.Guild.Id,
            LevelSettings = levelSetting,
            Value = value.ToString()
        });

        await ctx.EditReplyAsync(message: $"Updated {levelSetting.GetName()} level setting to {value}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.Member.Mention} updated {levelSetting.GetName()} level setting to {value}");
    }

    [SlashCommand("LogSet", "Set the leveling log channel.")]
    public async Task LogSetAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to change the log to.")] DiscordChannel? channel = null)
    {
        await ctx.DeferAsync();
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(Permissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }
        var response = await this._mediator.Send(new SetLevelSettingsCommand
        {
            GuildId = ctx.Guild.Id,
            LevelSettings = LevelSettings.LogChannel,
            Value = channel is null ? "0" : channel.Id.ToString()
        });
        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: $"Disabled the level log.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the level log.");
            return;
        }
        await ctx.EditReplyAsync(message: $"Updated the level log to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.User.Mention} updated the level log to {channel?.Mention}.");
    }
}
