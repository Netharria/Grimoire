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
using Grimoire.Settings.Domain;

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

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        if (!guildSettings.IgnoredRoles.Any() && !guildSettings.IgnoredChannels.Any() &&
            !guildSettings.IgnoredMembers.Any())
            throw new AnticipatedException("This server does not have any ignored channels, roles or users.");


        var embed = new DiscordEmbedBuilder()
            .WithTitle("Ignored Channels Roles and Users.")
            .WithTimestamp(DateTime.UtcNow);
        var embedPages = InteractivityExtension.GeneratePagesInEmbed(
            await BuildMessageAsync(ctx, guildSettings),
            SplitType.Line,
            embed);
        await ctx.Interaction.SendPaginatedResponseAsync(false, ctx.User, embedPages);
    }

    private static async Task<string> BuildMessageAsync(SlashCommandContext ctx, GuildSettings guildSettings)
    {
        ArgumentNullException.ThrowIfNull(ctx.Guild);
        var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
        foreach (var channel in guildSettings.IgnoredChannels)
        {
            var discordChannel = await ctx.Guild.GetChannelOrDefaultAsync(channel.Id);
            if (discordChannel is null)
                continue;
            ignoredMessageBuilder.Append(discordChannel.Mention).Append('\n');
        }


        ignoredMessageBuilder.Append("\n**Roles**\n");

        foreach (var role in guildSettings.IgnoredRoles)
        {
            var discordRole = await ctx.Guild.GetRoleOrDefaultAsync(role.Id);
            if (discordRole is null)
                continue;
            ignoredMessageBuilder.Append(discordRole.Mention).Append('\n');
        }

        ignoredMessageBuilder.Append("\n**Users**\n");

        foreach (var member in guildSettings.IgnoredMembers)
        {
            var user = await ctx.Client.GetUserOrDefaultAsync(member.Id);
            if (user is null)
                continue;
            ignoredMessageBuilder.Append(user.Mention).Append('\n');
        }

        return ignoredMessageBuilder.ToString();
    }
}
