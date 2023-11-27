// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Shared.Commands;
using Grimoire.Core.Features.Shared.Queries;
using Grimoire.Discord.Enums;

namespace Grimoire.Discord.SharedModule;

[SlashCommandGroup("ModLog", "View or set the moderation log channel.")]
[SlashRequireGuild]
[SlashRequireUserGuildPermissions(Permissions.ManageGuild)]
public class ModLogSettings(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("View", "View the current moderation log channel.")]
    public async Task ViewAsync(InteractionContext ctx)
    {
        var response = await this._mediator.Send(new GetModLogQuery
        {
            GuildId = ctx.Guild.Id
        });
        if (response.LogChannelId is null)
        {
            await ctx.ReplyAsync(message: "The moderation log is currently disabled.");
            return;
        }

        var channel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);
        if (channel is null)
        {
            await ctx.ReplyAsync(message: $"The current channel({response.LogChannelId}) for the moderation log could not be found. " +
                $"The channel might have been deleted.");
            return;
        }
        await ctx.ReplyAsync(message: $"The current moderation log channel is {channel.Mention}");
    }

    [SlashCommand("Set", "Set the moderation log channel.")]
    public async Task BanLogAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")] ChannelOption option,
        [Option("Channel", "The channel to send to send the logs to.")] DiscordChannel? channel = null)
    {
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(Permissions.SendMessages))
                throw new AnticipatedException($"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }
        var response = await this._mediator.Send(new SetModLogCommand
        {
            GuildId = ctx.Guild.Id,
            ChannelId = channel?.Id
        });
        if (option is ChannelOption.Off)
        {
            await ctx.ReplyAsync(message: $"Disabled the moderation log.", ephemeral: false);
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the level log.");
        }
        await ctx.ReplyAsync(message: $"Updated the moderation log to {channel?.Mention}", ephemeral: false);
        await ctx.SendLogAsync(response, GrimoireColor.Purple, message: $"{ctx.User.Mention} updated the moderation log to {channel?.Mention}.");
    }
}
