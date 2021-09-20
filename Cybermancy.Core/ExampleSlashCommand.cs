using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;

namespace Cybermancy.Core
{
    public class ExampleSlashCommand : ApplicationCommandModule
    {

        [SlashCommand("ping", "Checks the current connection status of the bot,")]
        public async Task TestCommand(InteractionContext ctx)
        {

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent($"Pong: {ctx.Client.Ping}ms").AsEphemeral(true));
        }
        
        [SlashCommand("delaytest", "A slash command made to test the DSharpPlusSlashCommands library!")]
        public async Task DelayTestCommand(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
    
            //Some time consuming task like a database call or a complex operation

            await Task.Delay(10 * 1000);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Thanks for waiting!"));
        }

        [SlashCommand("PageTest", "A slash command made to test the DSharpPlusSlashCommands library!")]
        public async Task PageTestCommand(InteractionContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivity();
            var pageBuild = new StringBuilder();
            for (var i = 0; i < 7000; i++)
            {
                pageBuild.Append($"This is item number {i}\n");
            }
            var embedPages = interactivity.GeneratePagesInEmbed(pageBuild.ToString(), SplitType.Line);
            await interactivity.SendPaginatedResponseAsync(ctx.Interaction, true, ctx.Member, embedPages);
        }
    }
}