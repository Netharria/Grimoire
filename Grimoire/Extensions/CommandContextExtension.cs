// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Diagnostics;
using LanguageExt;
using LanguageExt.Common;

namespace Grimoire.Extensions;

public static class CommandContextExtension
{
    public static async Task EditReplyAsync(
        this CommandContext ctx,
        DiscordColor? color = null,
        string message = "",
        string title = "",
        string footer = "",
        DiscordEmbed? embed = null,
        DateTime? timeStamp = null)
    {
        timeStamp ??= DateTime.UtcNow;
        embed ??= new DiscordEmbedBuilder()
            .WithColor(color ?? GrimoireColor.Purple)
            .WithAuthor(title)
            .WithDescription(message)
            .WithFooter(footer)
            .WithTimestamp(timeStamp)
            .Build();

        await DiscordRetryPolicy.RetryDiscordCall(async _ =>
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(embed)));
    }

    public static async Task SendErrorResponseAsync(
        this CommandContext ctx,
        string message = "")
    {
        var embed = new DiscordEmbedBuilder()
            .WithColor(GrimoireColor.Red)
            .WithDescription(message)
            .Build();

        await DiscordRetryPolicy.RetryDiscordCall(async _ =>
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().AddEmbed(embed)));
    }

    public static Either<Error, DiscordChannel?> GetChannelOption(this CommandContext ctx, ChannelOption channelOption,
        DiscordChannel? selectedChannel)
    {
        switch (channelOption)
        {
            case ChannelOption.Off:
                return (DiscordChannel?) null;
            case ChannelOption.CurrentChannel:
                return ctx.Channel;
            case ChannelOption.SelectChannel:
                if (selectedChannel is not null)
                    return selectedChannel;
                return Error.New(new ArgumentNullException(nameof(selectedChannel), "Selected channel cannot be empty when ChannelOption is SelectChannel."));
            default:
                return Error.New(new UnreachableException("Invalid ChannelOption value."));
        }
    }
}
