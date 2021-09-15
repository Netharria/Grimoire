using Cybermancy.Core.Enums;
using Cybermancy.Core.Utilities;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybermancy.Core.Extensions
{
    public static class InteractionContextExtension
    {
        public static async Task Reply(this InteractionContext ctx, CybermancyColor color = CybermancyColor.Purple, string message = null, 
            string title = null, string footer = null, DiscordEmbed embed = null, DateTime? timeStamp = null, bool ephemeral = true)
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
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(embed).AsEphemeral(ephemeral));

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
