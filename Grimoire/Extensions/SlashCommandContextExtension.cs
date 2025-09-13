// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


namespace Grimoire.Extensions;

public static class SlashCommandContextExtension
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

    public static DiscordChannel? GetChannelOptionAsync(this CommandContext ctx, ChannelOption channelOption,
        DiscordChannel? selectedChannel)
    {
        switch (channelOption)
        {
            case ChannelOption.Off:
                return null;
            case ChannelOption.CurrentChannel:
                return ctx.Channel;
            case ChannelOption.SelectChannel:
                if (selectedChannel is not null)
                    return selectedChannel;
                throw new AnticipatedException("Please specify a channel.");
            default:
                throw new AnticipatedException("Options selected are not valid.");
        }
    }
}
