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
    public async Task ShowIgnoredAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;


        var embed = new DiscordEmbedBuilder()
            .WithTitle("Ignored Channels Roles and Users.")
            .WithTimestamp(DateTime.UtcNow);
        var embedPages = InteractivityExtension.GeneratePagesInEmbed(
            await BuildMessageAsync(guild.GetGuildId()),
            SplitType.Line,
            embed);
        if (ctx is SlashCommandContext slashContext)
            await slashContext.Interaction.SendPaginatedResponseAsync(false, ctx.User, embedPages);
        else
            await ctx.Channel.SendPaginatedMessageAsync(ctx.User, embedPages);
    }

    private async Task<string> BuildMessageAsync(GuildId guildId)
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
            ignoredItems.IgnoredMemberIds.Select(x => $"<@&{x}>"))).Append('\n');
        return ignoredMessageBuilder.ToString();
    }
}
