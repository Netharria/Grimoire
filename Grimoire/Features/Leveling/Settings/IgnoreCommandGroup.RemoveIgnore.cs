// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Features.Leveling.Settings;

public partial class IgnoreCommandGroup
{
    [Command("Remove")]
    [Description("Removes a user, channel, or role from the ignored xp list.")]
    public async Task WatchAsync(CommandContext ctx,
        [Parameter("Item")] [Description("The user, channel or role to remove from the ignore xp list.")]
        params SnowflakeObject[] value)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");


        if (value.Length == 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var guildSettings = await this._settingsModule.GetGuildSettings(ctx.Guild.Id);

        var membersNoLongerIgnored =
            RemoveIgnoredItems<IgnoredMember, DiscordUser>(guildSettings.IgnoredMembers, value, ctx.Guild.Id);
        var rolesNoLongerIgnored =
            RemoveIgnoredItems<IgnoredRole, DiscordRole>(guildSettings.IgnoredRoles, value, ctx.Guild.Id);
        var channelsNoLongerIgnored =
            RemoveIgnoredItems<IgnoredChannel, DiscordChannel>(guildSettings.IgnoredChannels, value, ctx.Guild.Id);

        if (membersNoLongerIgnored.Length == 0 && rolesNoLongerIgnored.Length == 0 &&
            channelsNoLongerIgnored.Length == 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "All items in list provided were not ignored");
            return;
        }

        await this._settingsModule.UpdateGuildSettings(guildSettings);

        var messageBuilder =
            await BuildIgnoreListAsync(ctx, channelsNoLongerIgnored, rolesNoLongerIgnored, membersNoLongerIgnored);

        var message = messageBuilder
            .Append(" are no longer ignored for xp gain.")
            .ToString();

        await ctx.EditReplyAsync(GrimoireColor.Green,
            message);
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.DarkPurple,
            Description = message
        });
    }

    private static T[] RemoveIgnoredItems<T, T1>(ICollection<T> currentIgnoredItems, SnowflakeObject[] ignoredIds,
        ulong guildId) where T : IIgnored, new() where T1 : SnowflakeObject
    {
        if (ignoredIds.Length == 0)
            return [];
        if (!ignoredIds.OfType<T1>().Any())
            return [];
        var itemsRequestedToRemoveIgnore = ignoredIds
            .OfType<T1>()
            .Select(x => x.Id)
            .ToHashSet();

        var itemsToNoLongerIgnore = currentIgnoredItems
            .Where(x => itemsRequestedToRemoveIgnore.Contains(x.Id))
            .ToArray();

        foreach (var itemToNoLongerIgnore in itemsToNoLongerIgnore)
            currentIgnoredItems.Remove(itemToNoLongerIgnore);
        return itemsToNoLongerIgnore;
    }
}
