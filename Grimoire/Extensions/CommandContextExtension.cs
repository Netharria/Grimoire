// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using DSharpPlus.Commands.Processors.TextCommands;

namespace Grimoire.Extensions;

public static class CommandContextExtension
{
    extension(CommandContext ctx)
    {
        public ValueTask ReplyAsync(DiscordColor? color = null,
            string message = "",
            string title = "",
            string footer = "",
            DiscordEmbed? embed = null,
            DateTime? timeStamp = null,
            bool ephemeral = false)
        {
            timeStamp ??= DateTime.UtcNow;
            embed ??= new DiscordEmbedBuilder()
                .WithColor(color ?? GrimoireColor.Purple)
                .WithAuthor(title)
                .WithDescription(message)
                .WithFooter(footer)
                .WithTimestamp(timeStamp)
                .Build();

            return ctx switch
            {
                SlashCommandContext
                    {
                        Interaction.ResponseState: DiscordInteractionResponseState.Unacknowledged
                    } slashCommandContext
                    => DiscordRetryPolicy.RetryDiscordCall(async _ =>
                        await slashCommandContext.RespondAsync(embed, ephemeral)),
                TextCommandContext { Response: null }
                    => DiscordRetryPolicy.RetryDiscordCall(async _ => await ctx.RespondAsync(embed)),
                _ => DiscordRetryPolicy.RetryDiscordCall(async _ => { await ctx.EditResponseAsync(embed); })
            };
        }

        public ValueTask SendErrorResponseAsync(string message) =>
            ctx.ReplyAsync(GrimoireColor.Red, message, ephemeral: true);

        public ValueTask SendWarningResponseAsync(string message) =>
            ctx.ReplyAsync(GrimoireColor.Yellow, message, ephemeral: true);

        [Pure]
        public DiscordChannel? GetChannelOption(ChannelOption channelOption,
            DiscordChannel? selectedChannel)
        {
            return channelOption switch
            {
                ChannelOption.Off => null,
                ChannelOption.CurrentChannel => ctx.Channel,
                ChannelOption.SelectChannel => selectedChannel ?? throw new ArgumentNullException(
                    nameof(selectedChannel),
                    "Selected channel cannot be empty when ChannelOption is SelectChannel."),
                _ => throw new UnreachableException("Invalid ChannelOption value.")
            };
        }

        [Pure]
        public ModeratorId GetModeratorId() => new(ctx.User.Id);

        [Pure]
        public ChannelId GetChannelId() => new(ctx.Channel.Id);
    }
}
