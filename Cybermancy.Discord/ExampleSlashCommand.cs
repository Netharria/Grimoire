// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;

namespace Cybermancy.Discord
{
    /// <summary>
    /// Example commands used as simple versions of using the DSharpPlus slash commands in different ways.
    /// </summary>
    public class ExampleSlashCommand : ApplicationCommandModule
    {
        /// <summary>
        /// Gets the current ping for the bot.
        /// </summary>
        /// <param name="ctx">The context which triggered the interaction.</param>
        /// <returns>The completed task.</returns>
        [SlashCommand("ping", "Checks the current connection status of the bot,")]
        public static Task PingAsync(InteractionContext ctx)
        {
            return ctx.CreateResponseAsync(
                InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .WithContent($"Pong: {ctx.Client.Ping}ms")
                    .AsEphemeral(ephemeral: true));
        }

        /// <summary>
        /// A sample command that shows how to handle an interaction with a long processing time.
        /// </summary>
        /// <param name="ctx">The context which triggered the interaction.</param>
        /// <returns>The completed task.</returns>
        [SlashCommand("delaytest", "A slash command made to test the DSharpPlusSlashCommands library!")]
        public static async Task DelayTestCommandAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            // Some time consuming task like a database call or a complex operation
            await Task.Delay(10 * 1000);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Thanks for waiting!"));
        }

        /// <summary>
        /// A sample command that shows how to send a paginated response to an interaction.
        /// </summary>
        /// <param name="ctx">The context which triggered the interaction.</param>
        /// <returns>The completed task.</returns>
        [SlashCommand("PageTest", "A slash command made to test the DSharpPlusSlashCommands library!")]
        public static Task PageTestCommandAsync(InteractionContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();
            var pageBuild = new StringBuilder();
            for (var i = 0; i < 7000; i++)
                pageBuild.Append("This is item number ").Append(i).Append('\n');

            var embedPages = interactivity.GeneratePagesInEmbed(pageBuild.ToString(), SplitType.Line);
            return interactivity.SendPaginatedResponseAsync(ctx.Interaction, ephemeral: true, ctx.Member, embedPages);
        }
    }
}
