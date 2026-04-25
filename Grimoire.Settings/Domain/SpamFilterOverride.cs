// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public enum SpamFilterOverrideOption
{
    AlwaysFilter,
    NeverFilter,
    Inherit
}

public sealed record SpamFilterOverride
{
    private SpamFilterOverride(
        SpamFilterOverrideOption channelOption,
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt)
    {
        ChannelOption = channelOption;
        ChannelId = channelId;
        GuildId = guildId;
        SetBy = setBy;
        SetAt = setAt;
    }

    public SpamFilterOverrideOption ChannelOption { get; }
    public ChannelId ChannelId { get; }
    public GuildId GuildId { get; }
    public ModeratorId SetBy { get; }
    public DateTimeOffset SetAt { get; }

    public static Validation<SpamFilterOverride> Create(
        SpamFilterOverrideOption channelOption,
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt)
    {
        if (channelId.Value == 0)
            return Validation<SpamFilterOverride>.Fail(
                new Error("spam-filter-override.channel-id.invalid", "ChannelId must be specified."));
        if (guildId.Value == 0)
            return Validation<SpamFilterOverride>.Fail(
                new Error("spam-filter-override.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<SpamFilterOverride>.Fail(
                new Error("spam-filter-override.set-by.invalid", "ModeratorId must be specified."));
        return Validation<SpamFilterOverride>.Succeed(
            new SpamFilterOverride(channelOption, channelId, guildId, setBy, setAt));
    }
}
