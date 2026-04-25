// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public enum MessageLogOverrideOption
{
    AlwaysLog,
    NeverLog,
    Inherit
}

public sealed record MessageLogChannelOverride
{
    private MessageLogChannelOverride(
        MessageLogOverrideOption channelOption,
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

    public MessageLogOverrideOption ChannelOption { get; }
    public ChannelId ChannelId { get; }
    public GuildId GuildId { get; }
    public ModeratorId SetBy { get; }
    public DateTimeOffset SetAt { get; }

    public static Validation<MessageLogChannelOverride> Create(
        MessageLogOverrideOption channelOption,
        ChannelId channelId,
        GuildId guildId,
        ModeratorId setBy,
        DateTimeOffset setAt)
    {
        if (channelId.Value == 0)
            return Validation<MessageLogChannelOverride>.Fail(
                new Error("message-log-override.channel-id.invalid", "ChannelId must be specified."));
        if (guildId.Value == 0)
            return Validation<MessageLogChannelOverride>.Fail(
                new Error("message-log-override.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<MessageLogChannelOverride>.Fail(
                new Error("message-log-override.set-by.invalid", "ModeratorId must be specified."));
        return Validation<MessageLogChannelOverride>.Succeed(
            new MessageLogChannelOverride(channelOption, channelId, guildId, setBy, setAt));
    }
}
