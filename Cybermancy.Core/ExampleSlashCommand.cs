using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace Cybermancy.Core
{
    public class ExampleSlashCommand : ApplicationCommandModule
    {
        // private DiscordClient _discordClient;
        //
        // public ExampleSlashCommand(DiscordClient discordClient)
        // {
        //     _discordClient = discordClient;
        // }
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
    }
}