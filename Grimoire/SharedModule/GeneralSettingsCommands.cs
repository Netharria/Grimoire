// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Commands;
using Grimoire.Features.Shared.Queries;

namespace Grimoire.SharedModule;

[SlashCommandGroup("GeneralSettings", "View or set general settings.")]
[SlashRequireGuild]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
internal sealed class GeneralSettingsCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("View", "View the current general settings.")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);
        var response = await this._mediator.Send(new GetGeneralSettings.Query
        {
            GuildId = ctx.Guild.Id
        });
        var moderationLogText = response.ModLogChannel is null ? "None" : ChannelExtensions.Mention(response.ModLogChannel.Value);
        var userCommandChannelText = response.UserCommandChannel is null ? "None" : ChannelExtensions.Mention(response.UserCommandChannel.Value);
        await ctx.EditReplyAsync(title: "General Settings", message: $"**Moderation Log:** {moderationLogText}\n**User Command Channel:** {userCommandChannelText}");
    }

    [SlashCommand("ModLogChannel", "Set the moderation log channel.")]
    public async Task SetAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to send to send the logs to.")] DiscordChannel? channel = null)
    {
        await ctx.DeferAsync();
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }
        var response = await this._mediator.Send(new SetModLogCommand
        {
            GuildId = ctx.Guild.Id,
            ChannelId = channel?.Id
        });
        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: $"Disabled the moderation log.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the level log.");
            return;
        }
        await ctx.EditReplyAsync(message: $"Updated the moderation log to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} updated the moderation log to {channel?.Mention}.");
    }

    [SlashCommand("UserCommands", "Set the channel where some commands are visible for non moderators.")]
    public async Task SetUserCommandChannelAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to send to send the logs to.")] DiscordChannel? channel = null)
    {
        await ctx.DeferAsync();
        channel = ctx.GetChannelOptionAsync(option, channel);
        var response = await this._mediator.Send(new SetUserCommandChannel.Command
        {
            GuildId = ctx.Guild.Id,
            ChannelId = channel?.Id
        });
        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: $"Disabled the User Command Channel.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the User Command Channel.");
            return;
        }
        await ctx.EditReplyAsync(message: $"Updated the User Command Channel to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} updated the User Command Channel to {channel?.Mention}.");
    }
}
