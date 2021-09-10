using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace Cybermancy.Core
{
    public class ExampleSlashCommand : ApplicationCommandModule
    {
        [SlashCommand("test", "A slash command made to test the DSharpPlusSlashCommands library!")]
        public async Task TestCommand(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent("Success!"));
        }
    }
}