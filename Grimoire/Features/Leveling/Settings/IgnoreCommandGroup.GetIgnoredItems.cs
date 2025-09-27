// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class IgnoreCommandGroup
{
    [Command("View")]
    [Description("Displays all ignored users, channels and roles on this server.")]
    public async Task ShowIgnoredAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");


        var embed = new DiscordEmbedBuilder()
            .WithTitle("Ignored Channels Roles and Users.")
            .WithTimestamp(DateTime.UtcNow);
        var embedPages = InteractivityExtension.GeneratePagesInEmbed(
            await BuildMessageAsync(ctx.Guild.Id),
            SplitType.Line,
            embed);
        await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.User, embedPages);
    }

    private async Task<string> BuildMessageAsync(ulong guildId)
    {
        var ignoredItems = await this._settingsModule.GetAllIgnoredItems(guildId);
        var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
        ignoredMessageBuilder.Append(string.Join(' ',
            ignoredItems.IgnoredChannelIds.Select(x => $"<#{x}>"))).Append('\n');


        ignoredMessageBuilder.Append("\n**Roles**\n");
        ignoredMessageBuilder.Append(string.Join(' ',
            ignoredItems.IgnoredRoleIds.Select(x => $"<@&{x}>"))).Append('\n');

        ignoredMessageBuilder.Append("\n**Users**\n");
        ignoredMessageBuilder.Append(string.Join(' ',
            ignoredItems.IgnoredRoleIds.Select(x => $"<@&{x}>"))).Append('\n');
        return ignoredMessageBuilder.ToString();
    }
}
