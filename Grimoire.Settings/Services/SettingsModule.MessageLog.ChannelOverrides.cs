// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;
using Grimoire.Settings.Enums;

namespace Grimoire.Settings.Services;

public sealed partial class SettingsModule
{
    /// <param name="channelNodes">Each channel's parent, so an override on a category covers its channels and threads.</param>
    public async Task<Fin<bool>> ShouldLogMessage(ChannelId channelId,
        GuildId guildId,
        IReadOnlyDictionary<ChannelId, ChannelId?> channelNodes,
        CancellationToken cancellationToken = default)
    {
        if (!await IsModuleEnabled(Module.MessageLog, guildId, cancellationToken)
                .OrDefaultDuringMigration(false))
            return Fin.Succ<bool>(false);
        return await ReadDocument<MessageLogOverrides, bool>(guildId,
            overrides => overrides.ShouldLog(channelId, channelNodes),
            cancellationToken);
    }

    /// <summary>Sets the channel's override, or with <c>None</c> clears it, so the channel follows its parent.</summary>
    public Task<Fin<Write<Option<MessageLogOverrideOption>>>> SetChannelLogOverride(
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        Option<MessageLogOverrideOption> option,
        CancellationToken cancellationToken = default)
        => UpdateDocument<MessageLogOverrides, Option<MessageLogOverrideOption>>(guildId, setBy,
            overrides => (overrides.With(channelId, option), option),
            "The channel is already set to this log option.", cancellationToken,
            "Could not set channel log override.");

    /// <summary>Every channel with an override. A failure is returned, not an empty map (spec 2.5, OQ-4).</summary>
    public Task<Fin<Map<ChannelId, MessageLogOverrideOption>>> GetAllOverriddenChannels(GuildId guildId,
        CancellationToken cancellationToken = default)
        => ReadDocument<MessageLogOverrides, Map<ChannelId, MessageLogOverrideOption>>(guildId,
            overrides => overrides.Channels,
            cancellationToken);
}
