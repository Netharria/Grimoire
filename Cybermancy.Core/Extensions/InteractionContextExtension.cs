// -----------------------------------------------------------------------
// <copyright file="InteractionContextExtension.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Cybermancy.Core.Enums;
    using Cybermancy.Core.Utilities;
    using DSharpPlus;
    using DSharpPlus.Entities;
    using DSharpPlus.SlashCommands;

    public static class InteractionContextExtension
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="color"></param>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="footer"></param>
        /// <param name="embed"></param>
        /// <param name="timeStamp"></param>
        /// <param name="ephemeral"></param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
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
