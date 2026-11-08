// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using DSharpPlus.Commands.ArgumentModifiers;
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
    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public async Task WatchAsync(CommandContext ctx,
        [Parameter("channel")] [Description("The channel to remove from the ignore xp list.")]
        DiscordChannel? channel = null,
        [VariadicArgument(24)] [Parameter("item")] [Description("The users or roles to remove from the ignore xp list.")]
        SnowflakeObject[]? items = null)
    {
        await ctx.DeferResponseAsync();
        if (ctx.GetRequiredGuild() is not Validation<DiscordGuild>.Valid { Value: var guild })
        {
            await ctx.SendErrorResponseAsync("This command can only be used in a server.");
            return;
        }

        var value = CombineItems(channel, items);
        if (value.Length == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var ignoredItems = value.Select(item => (XpTrackedItem?)(item switch
            {
                DiscordUser => new WatchedMember
                {
                    UserId = new UserId(item.Id),
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.UtcNow,
                    SetBy = ctx.GetModeratorId()
                },
                DiscordRole => new WatchedRole
                {
                    RoleId = new RoleId(item.Id),
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.UtcNow,
                    SetBy = ctx.GetModeratorId()
                },
                DiscordChannel => new WatchedChannel
                {
                    ChannelId = new ChannelId(item.Id),
                    GuildId = guild.GetGuildId(),
                    SetAt = DateTimeOffset.UtcNow,
                    SetBy = ctx.GetModeratorId()
                },
                _ => null
            })).OfType<XpTrackedItem>()
            .ToFrozenSet();

        var message = BuildIgnoreListAsync(ignoredItems) + " are no longer ignored for xp gain.";
        await ctx.ReplyResultAsync(
            await this._settingsModule.AppendIgnoredItemsEvent(guild.GetGuildId(), ignoredItems),
            message,
            () => this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.DarkPurple,
                Description = message
            }),
            GrimoireColor.Green);
    }
}
