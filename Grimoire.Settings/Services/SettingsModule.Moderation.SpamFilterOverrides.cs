// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    /// <summary>The channel's own override. <c>None</c> means it follows its parent.</summary>
    public Task<Fin<Option<SpamFilterOverrideOption>>> GetSpamFilterOverrideAsync(
        GuildId guildId,
        ChannelId channelId,
        CancellationToken cancellationToken = default)
        => ReadDocument<SpamFilterOverrides, Option<SpamFilterOverrideOption>>(guildId,
            overrides => overrides.For(channelId),
            cancellationToken);

    /// <summary>Every channel with an override. A failure is returned, not an empty map (spec 2.5, OQ-4).</summary>
    public Task<Fin<Map<ChannelId, SpamFilterOverrideOption>>> GetAllSpamFilterOverrideAsync(GuildId guildId,
        CancellationToken cancellationToken = default)
        => ReadDocument<SpamFilterOverrides, Map<ChannelId, SpamFilterOverrideOption>>(guildId,
            overrides => overrides.Channels,
            cancellationToken);

    /// <summary>Sets the channel's override, or with <c>None</c> clears it, so the channel follows its parent.</summary>
    public Task<Fin<Write<Option<SpamFilterOverrideOption>>>> SetSpamFilterOverrideAsync(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        Option<SpamFilterOverrideOption> option,
        CancellationToken cancellationToken = default)
        => UpdateDocument<SpamFilterOverrides, Option<SpamFilterOverrideOption>>(guildId, setBy,
            overrides => (overrides.With(channelId, option), option),
            "The channel is already set to this spam filter option.", cancellationToken,
            "Could not save the spam filter override.");
}
