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

    private static string BuildIgnoreListAsync(
        IEnumerable<XpIgnoredItem> ignoredItems)
    {
        return string.Join(' ', ignoredItems
            .Select(item => item switch
            {
                IgnoredChannel c => $"<#{c.ChannelId.Value}>",
                IgnoredMember m => $"<@{m.UserId.Value}>",
                IgnoredRole r => $"<@&{r.RoleId.Value}>",
                _ => ""
            }));
    }
}
