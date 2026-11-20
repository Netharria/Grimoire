// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using LanguageExt;
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
    ///     <paramref name="write" /> is the settings write (ignore or unignore); <paramref name="outcome" /> finishes
    ///     the sentence after the changed mentions, and <paramref name="unchangedOutcome" /> the one after those
    ///     already in that state.
    /// </summary>
    private async Task UpdateIgnoredAsync(CommandContext ctx, DiscordChannel? channel, SnowflakeObject[]? items,
        Func<GuildId, ModeratorId, IReadOnlySet<XpTarget>, Task<Fin<Write<IReadOnlySet<XpTarget>>>>> write,
        string outcome, string unchangedOutcome)
    {
        await ctx.DeferResponseAsync();
        var guild = ctx.RequiredGuild;

        var combined = CombineItems(channel, items);
        if (combined.Length == 0)
        {
            await ctx.ReplyAsync(GrimoireColor.Yellow, "Could not parse any ids from the submitted values.");
            return;
        }

        var tracked = combined.Select(ToTarget).ToFrozenSet();
        await (await write(guild.GetGuildId(), ctx.GetModeratorId(), tracked)).Match(
            write => write.Match(
                saved: changed => ReportChangedAsync(changed),
                unchanged: _ => ctx.ReplyAsync(GrimoireColor.Yellow,
                    $"{BuildIgnoreList(tracked)} {unchangedOutcome}").AsTask()),
            error => ctx.SendErrorResponseAsync(error).AsTask());

        async Task ReportChangedAsync(IReadOnlySet<XpTarget> changed)
        {
            var message = $"{BuildIgnoreList(changed)} {outcome}";
            var unchanged = tracked.Except(changed).ToArray();
            await ctx.ReplyAsync(GrimoireColor.Green, unchanged.Length == 0
                ? message
                : $"{message}\n{BuildIgnoreList(unchanged)} {unchangedOutcome}");
            await this._guildLog.SendLogMessageAsync(new GuildLogTextMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.DarkPurple,
                Description = message
            });
        }
    }

    private static XpTarget ToTarget(SnowflakeObject item)
        => item switch
        {
            DiscordUser => new XpTarget.Member(new UserId(item.Id)),
            DiscordRole => new XpTarget.Role(new RoleId(item.Id)),
            DiscordChannel => new XpTarget.Channel(new ChannelId(item.Id)),
            _ => throw new InvariantViolationException($"Unexpected ignore option {item.GetType().Name}.")
        };

    private static string BuildIgnoreList(IEnumerable<XpTarget> items)
        => string.Join(' ', items.Select(item => item switch
        {
            XpTarget.Channel c => $"<#{c.Id.Value}>",
            XpTarget.Member m => $"<@{m.Id.Value}>",
            XpTarget.Role r => $"<@&{r.Id.Value}>",
            _ => throw new InvariantViolationException($"Unknown ignore item {item.GetType().Name}.")
        }));
}
