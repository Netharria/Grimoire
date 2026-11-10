// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Leveling.Settings;

[Command("Ignore")]
[Description("Commands for updating and viewing the server ignore list.")]
public sealed partial class IgnoreCommandGroup(SettingsModule settingsModule, GuildLog guildLog)
{
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;

    /// <summary>
    ///     Joins the channel and mentionable options. They're separate because Discord's mentionable options only offer
    ///     users and roles.
    /// </summary>
    private static SnowflakeObject[] CombineItems(DiscordChannel? channel, SnowflakeObject[]? items)
        => [.. channel is null ? [] : new SnowflakeObject[] { channel }, .. items ?? []];

    /// <summary>
    ///     Records the channel and items as ignored or no longer ignored, then replies and logs what changed.
    ///     <paramref name="create" /> builds the record for one item; <paramref name="outcome" /> finishes the sentence
    ///     after the changed mentions, and <paramref name="unchangedOutcome" /> the one after those already in that state.
    /// </summary>
    private async Task UpdateIgnoredAsync(CommandContext ctx, DiscordChannel? channel, SnowflakeObject[]? items,
        Func<SnowflakeObject, GuildId, ModeratorId, DateTimeOffset, Validation<XpTrackedItem>> create, string outcome,
        string unchangedOutcome)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var combined = CombineItems(channel, items);
        if (combined.Length == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var created = combined
            .Select(item => create(item, guild.GetGuildId(), ctx.GetModeratorId(), DateTimeOffset.UtcNow))
            .ToArray();
        if (created.OfType<Validation<XpTrackedItem>.Invalid>().SelectMany(invalid => invalid.Errors).ToArray()
            is [_, ..] errors)
        {
            await ctx.SendErrorResponseAsync(errors);
            return;
        }

        var tracked = created.OfType<Validation<XpTrackedItem>.Valid>().Select(valid => valid.Value).ToFrozenSet();
        await (await this._settingsModule.AppendIgnoredItemsEvent(guild.GetGuildId(), tracked)).Match(
            async changed =>
            {
                var message = $"{BuildIgnoreList(changed)} {outcome}";
                var unchanged = tracked.Except(changed).ToArray();
                await ctx.ReplyAsync(GrimoireColor.Green, unchanged.Length == 0
                    ? message
                    : $"{message}\n{BuildIgnoreList(unchanged)} {unchangedOutcome}");
                await this._guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = guild.GetGuildId(),
                    GuildLogType = GuildLogType.Moderation,
                    Color = GrimoireColor.DarkPurple,
                    Description = message
                });
            },
            error => ctx.SendErrorResponseAsync(error.Message).AsTask(),
            onNotModified: _ => ctx.ReplyAsync(GrimoireColor.Yellow,
                $"{BuildIgnoreList(tracked)} {unchangedOutcome}").AsTask());
    }

    private static string BuildIgnoreList(IEnumerable<XpTrackedItem> items)
        => string.Join(' ', items.Select(item => item switch
        {
            IgnoredChannel c => $"<#{c.ChannelId.Value}>",
            IgnoredMember m => $"<@{m.UserId.Value}>",
            IgnoredRole r => $"<@&{r.RoleId.Value}>",
            WatchedChannel c => $"<#{c.ChannelId.Value}>",
            WatchedMember m => $"<@{m.UserId.Value}>",
            WatchedRole r => $"<@&{r.RoleId.Value}>",
            _ => throw new InvariantViolationException($"Unknown ignore item {item.GetType().Name}.")
        }));
}
