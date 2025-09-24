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
    [Command("Add")]
    [Description("Adds a user, channel, or role to the ignored xp list.")]
    public async Task IgnoreAsync(CommandContext ctx,
        [Parameter("items")] [Description("The user, channel or role to ignore")]
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

        var newIgnoredUsers =
            AddedIgnoredItems<IgnoredMember, DiscordUser>(guildSettings.IgnoredMembers, value, ctx.Guild.Id);
        var newIgnoredRoles =
            AddedIgnoredItems<IgnoredRole, DiscordRole>(guildSettings.IgnoredRoles, value, ctx.Guild.Id);
        var newIgnoredChannels =
            AddedIgnoredItems<IgnoredChannel, DiscordChannel>(guildSettings.IgnoredChannels, value, ctx.Guild.Id);

        if (newIgnoredUsers.Length == 0 && newIgnoredRoles.Length == 0 && newIgnoredChannels.Length == 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "All items in list provided were already ignored");
            return;
        }

        await this._settingsModule.UpdateGuildSettings(guildSettings);

        var messageBuilder = await BuildIgnoreListAsync(ctx, newIgnoredChannels, newIgnoredRoles, newIgnoredUsers);

        var message = messageBuilder
            .Append(" are now ignored for xp gain.")
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

    private static T[] AddedIgnoredItems<T, T1>(ICollection<T> currentIgnoredItems, SnowflakeObject[] ignoredIds,
        ulong guildId) where T : IIgnored, new() where T1 : SnowflakeObject
    {
        if (ignoredIds.Length == 0)
            return [];
        if (!ignoredIds.OfType<T1>().Any())
            return [];
        var existingIgnoredMemberIds = currentIgnoredItems
            .Select(x => x.Id)
            .ToHashSet();

        var newUsersToIgnore = ignoredIds
            .OfType<T1>()
            .Where(x => !existingIgnoredMemberIds.Contains(x.Id))
            .Select(x => new T { Id = x.Id, GuildId = guildId })
            .ToArray();

        foreach (var newIgnoredMember in newUsersToIgnore)
            currentIgnoredItems.Add(newIgnoredMember);
        return newUsersToIgnore;
    }
}
