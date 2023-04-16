// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Cybermancy.Core.Responses;

namespace Cybermancy.Discord.Extensions
{
    public static class InteractionContextExtension
    {
        public static async Task ReplyAsync(
            this InteractionContext ctx,
            DiscordColor? color = null,
            string message = "",
            string title = "",
            string footer = "",
            DiscordEmbed? embed = null,
            DateTime? timeStamp = null,
            bool ephemeral = true)
        {
            timeStamp ??= DateTime.UtcNow;
            embed ??= new DiscordEmbedBuilder()
                .WithColor(color ?? CybermancyColor.Purple)
                .WithTitle(title)
                .WithDescription(message)
                .WithFooter(footer)
                .WithTimestamp(timeStamp)
                .Build();

            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral(ephemeral));
            
        }

        public static async ValueTask<(bool, ulong)> TryMatchStringToChannelAsync(this InteractionContext ctx, string s)
        {
            var parsedvalue = Regex.Match(s, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
            if (!ulong.TryParse(parsedvalue, out var parsedId))
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "Please give a valid channel.");
                return (false, 0);
            }
            if (!ctx.Guild.Channels.ContainsKey(parsedId) && parsedId != 0)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "Did not find that channel on this server.");
                return(false, 0);
            }

            return(true, parsedId);
        }

        public static async ValueTask<(bool, ulong)> TryMatchStringToChannelOrDefaultAsync(this InteractionContext ctx, string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return (true, ctx.Channel.Id);
            var parsedvalue = Regex.Match(s, @"(\d{17,21})", RegexOptions.None, TimeSpan.FromSeconds(1)).Value;
            if (!ulong.TryParse(parsedvalue, out var parsedId))
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "Please give a valid channel.");
                return (false, 0);
            }
            if (!ctx.Guild.Channels.ContainsKey(parsedId) && parsedId != 0)
            {
                await ctx.ReplyAsync(CybermancyColor.Orange, message: "Did not find that channel on this server.");
                return (false, 0);
            }

            return (true, parsedId);
        }

        public static async Task SendLogAsync(this InteractionContext ctx,
            BaseResponse response,
            DiscordColor? color,
            string? message = null)
        {
            message ??= response.Message;
            if (response.LogChannelId is null) return;
            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

            if (logChannel is null) return;
            var embed = new DiscordEmbedBuilder()
                .WithColor(color ?? CybermancyColor.Purple)
                .WithDescription(message)
                .WithTimestamp(DateTime.UtcNow)
                .Build();

            await ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(embed));

        }
    }
}
