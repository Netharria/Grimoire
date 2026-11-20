// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain.Documents;

/// <summary>A channel's message log override. No override means the channel follows its parent (spec 2.5).</summary>
public enum MessageLogOverrideOption
{
    AlwaysLog,
    NeverLog
}

/// <summary>A channel's spam filter override. No override means the channel follows its parent (spec 2.5).</summary>
public enum SpamFilterOverrideOption
{
    AlwaysFilter,
    NeverFilter
}

/// <summary>The channels whose messages are always or never logged (C14, spec 2.5).</summary>
public sealed record MessageLogOverrides : ISettingsDocument<MessageLogOverrides>
{
    public Map<ChannelId, MessageLogOverrideOption> Channels { get; init; }

    public static SettingsDocument Kind => SettingsDocument.MessageLogOverrides;
    public static MessageLogOverrides Empty { get; } = new();

    public Option<MessageLogOverrideOption> For(ChannelId channel) => this.Channels.Find(channel);

    /// <summary>Sets the channel's override, or with <c>None</c> clears it.</summary>
    public MessageLogOverrides With(ChannelId channel, Option<MessageLogOverrideOption> option)
        => this with { Channels = ChannelOverrides.With(this.Channels, channel, option) };

    /// <summary>
    ///     The first override from the channel up through its parents decides, and with none the message is logged.
    ///     Total: it stops after as many steps as there are channels, so a loop in the map can't hang it.
    /// </summary>
    public bool ShouldLog(ChannelId channel, IReadOnlyDictionary<ChannelId, ChannelId?> parents)
        => ChannelOverrides.Nearest(this.Channels, channel, parents)
            .Match(Some: option => option is MessageLogOverrideOption.AlwaysLog, None: () => true);
}

/// <summary>The channels whose messages are always or never checked for spam (C14, spec 2.5).</summary>
public sealed record SpamFilterOverrides : ISettingsDocument<SpamFilterOverrides>
{
    public Map<ChannelId, SpamFilterOverrideOption> Channels { get; init; }

    public static SettingsDocument Kind => SettingsDocument.SpamFilterOverrides;
    public static SpamFilterOverrides Empty { get; } = new();

    public Option<SpamFilterOverrideOption> For(ChannelId channel) => this.Channels.Find(channel);

    /// <summary>Sets the channel's override, or with <c>None</c> clears it.</summary>
    public SpamFilterOverrides With(ChannelId channel, Option<SpamFilterOverrideOption> option)
        => this with { Channels = ChannelOverrides.With(this.Channels, channel, option) };
}

internal static class ChannelOverrides
{
    internal static Map<ChannelId, T> With<T>(Map<ChannelId, T> channels, ChannelId channel, Option<T> option)
        => option.Match(Some: value => channels.AddOrUpdate(channel, value), None: () => channels.Remove(channel));

    /// <summary>The override nearest the channel, walking up through its parents.</summary>
    internal static Option<T> Nearest<T>(
        Map<ChannelId, T> channels, ChannelId channel, IReadOnlyDictionary<ChannelId, ChannelId?> parents)
    {
        ChannelId? current = channel;
        for (var step = 0; current is { } id && step <= parents.Count; step++)
        {
            var found = channels.Find(id);
            if (found.IsSome)
                return found;
            current = parents.GetValueOrDefault(id);
        }

        return None;
    }
}
