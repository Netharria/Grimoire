// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Cybermancy.Core.Enums;
using Cybermancy.Core.Utilities;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace Cybermancy.Core.Extensions
{
    public static class InteractionContextExtension
    {
        public static async Task ReplyAsync(
            this InteractionContext ctx,
            CybermancyColor color = CybermancyColor.Purple,
            string message = null,
            string title = null,
            string footer = null,
            DiscordEmbed embed = null,
            DateTime? timeStamp = null,
            bool ephemeral = true)
        {
            timeStamp ??= DateTime.UtcNow;
            embed ??= new DiscordEmbedBuilder()
                .WithColor(ColorUtility.GetColor(color))
                .WithTitle(title)
                .WithDescription(message)
                .WithFooter(footer)
                .WithTimestamp(timeStamp)
                .Build();
            try
            {
                await ctx.CreateResponseAsync(
                    InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral(ephemeral));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
