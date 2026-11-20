// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Enums;
using LanguageExt;

namespace Grimoire.Features.Leveling.Settings;

public sealed partial class IgnoreCommandGroup
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("View")]
    [Description("Displays all ignored users, channels and roles on this server.")]
    public async Task ShowIgnoredAsync(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.RequiredGuild;

        if (await this._settingsModule.GetAllIgnoredItems(guild.GetGuildId())
            is not Fin<IReadOnlySet<XpTarget>>.Succ { Value: var ignoredItems })
        {
            await ctx.SendErrorResponseAsync("Could not load the ignored items. Please try again later.");
            return;
        }

        if (ignoredItems.Count == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.DarkPurple, title: "Ignored Channels Roles and Users.",
                message: "This server does not have any ignored channels, roles or users.");
            return;
        }

        await ctx.ReplyPagedAsync(
            new DiscordEmbedBuilder().WithTitle("Ignored Channels Roles and Users.").WithTimestamp(DateTime.UtcNow),
            BuildMessage(ignoredItems).TrimEnd('\n').Split('\n'));
    }

    private static string BuildMessage(IReadOnlySet<XpTarget> ignoredItems)
    {
        var ignoredMessageBuilder = new StringBuilder().Append("**Channels**\n");
        ignoredMessageBuilder.Append(string.Join('\n',
            ignoredItems.OfType<XpTarget.Channel>().Select(x => $"<#{x.Id.Value}>"))).Append('\n');


        ignoredMessageBuilder.Append("\n**Roles**\n");
        ignoredMessageBuilder.Append(string.Join('\n',
            ignoredItems.OfType<XpTarget.Role>().Select(x => $"<@&{x.Id.Value}>"))).Append('\n');

        ignoredMessageBuilder.Append("\n**Users**\n");
        ignoredMessageBuilder.Append(string.Join('\n',
            ignoredItems.OfType<XpTarget.Member>().Select(x => $"<@{x.Id.Value}>"))).Append('\n');
        return ignoredMessageBuilder.ToString();
    }
}
