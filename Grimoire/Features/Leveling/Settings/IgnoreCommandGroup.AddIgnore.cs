// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Leveling.Settings;

public partial class IgnoreCommandGroup
{
    [RequireGuild]
    [RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
    [RequireModuleEnabled(Module.Leveling)]
    [Command("Add")]
    [Description("Adds a user, channel, or role to the ignored xp list.")]
    public async Task IgnoreAsync(CommandContext ctx,
        [Parameter("items")] [Description("The user, channel or role to ignore")]
        params SnowflakeObject[] value)
    {
        await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        if (value.Length == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var ignoredItems = value.Select(item => (XpIgnoredItem?)(item switch
            {
                DiscordUser => new IgnoredMember
                {
                    Id = item.Id,
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.Now,
                    SetBy = ctx.GetModeratorId(),
                    Enabled = true
                },
                DiscordRole => new IgnoredRole
                {
                    Id = item.Id,
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.Now,
                    SetBy = ctx.GetModeratorId(),
                    Enabled = true
                },
                DiscordChannel => new IgnoredChannel
                {
                    Id = item.Id,
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.Now,
                    SetBy = ctx.GetModeratorId(),
                    Enabled = true
                },
                _ => null
            })).OfType<XpIgnoredItem>()
            .ToFrozenSet();

        await this._settingsModule.AppendIgnoredItemsEvent(
            guild.GetGuildId(),
            ignoredItems
            );
        var message = BuildIgnoreListAsync(ignoredItems) +
                      " are now ignored for xp gain.";

        await ctx.ReplyAsync(GrimoireColor.Green,
            message);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.GetGuildId(),
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = message
        });
    }
}
