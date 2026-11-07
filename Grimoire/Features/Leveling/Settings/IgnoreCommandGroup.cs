// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
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

    private static string BuildIgnoreListAsync(
        IEnumerable<XpTrackedItem> ignoredItems)
    {
        return string.Join(' ', ignoredItems
            .Select(item => item switch
            {
                IgnoredChannel c => $"<#{c.ChannelId.Value}>",
                IgnoredMember m => $"<@{m.UserId.Value}>",
                IgnoredRole r => $"<@&{r.RoleId.Value}>",
                WatchedChannel c => $"<#{c.ChannelId.Value}>",
                WatchedMember m => $"<@{m.UserId.Value}>",
                WatchedRole r => $"<@&{r.RoleId.Value}>",
                _ => ""
            }));
    }
}
