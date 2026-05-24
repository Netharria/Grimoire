// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

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
        if (ctx.GetRequiredGuild() is not Validation<DiscordGuild>.Valid { Value: var guild })
        {
            await ctx.SendErrorResponseAsync("This command can only be used in a server.");
            return;
        }

        if (value.Length == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var ignoredItems = value.Select(item => (XpTrackedItem?)(item switch
            {
                DiscordUser => new IgnoredMember
                {
                    UserId = new UserId(item.Id),
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.Now,
                    SetBy = ctx.GetModeratorId()
                },
                DiscordRole => new IgnoredRole
                {
                    RoleId = new RoleId(item.Id),
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.Now,
                    SetBy = ctx.GetModeratorId()
                },
                DiscordChannel => new IgnoredChannel
                {
                    ChannelId = new ChannelId(item.Id),
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.Now,
                    SetBy = ctx.GetModeratorId()
                },
                _ => null
            })).OfType<XpTrackedItem>()
            .ToHashSet();

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
