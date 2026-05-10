// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public abstract record XpTrackedItem
{
    protected ulong Id { get; init; }
    public required GuildId GuildId { get; init; }
    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public abstract record XpIgnoredItem : XpTrackedItem;

public abstract record XpWatchedItem : XpTrackedItem;

public sealed record IgnoredChannel : XpIgnoredItem
{
    public ChannelId ChannelId { get => new(Id); init => Id = value.Value; }

    public static Validation<IgnoredChannel> Create(
        ChannelId channelId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (channelId.Value == 0)
            return Validation<IgnoredChannel>.Fail(
                new Error("xp-ignored-channel.channel-id.invalid", "ChannelId must be specified."));
        if (guildId.Value == 0)
            return Validation<IgnoredChannel>.Fail(
                new Error("xp-ignored-channel.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<IgnoredChannel>.Fail(
                new Error("xp-ignored-channel.set-by.invalid", "ModeratorId must be specified."));
        return Validation<IgnoredChannel>.Succeed(
            new IgnoredChannel { ChannelId = channelId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}

public sealed record WatchedChannel : XpWatchedItem
{
    public ChannelId ChannelId { get => new(Id); init => Id = value.Value; }

    public static Validation<WatchedChannel> Create(
        ChannelId channelId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (channelId.Value == 0)
            return Validation<WatchedChannel>.Fail(
                new Error("xp-watched-channel.channel-id.invalid", "ChannelId must be specified."));
        if (guildId.Value == 0)
            return Validation<WatchedChannel>.Fail(
                new Error("xp-watched-channel.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<WatchedChannel>.Fail(
                new Error("xp-watched-channel.set-by.invalid", "ModeratorId must be specified."));
        return Validation<WatchedChannel>.Succeed(
            new WatchedChannel { ChannelId = channelId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}

public sealed record IgnoredMember : XpIgnoredItem
{
    public UserId UserId { get => new(Id); init => Id = value.Value; }

    public static Validation<IgnoredMember> Create(
        UserId userId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (userId.Value == 0)
            return Validation<IgnoredMember>.Fail(
                new Error("xp-ignored-member.user-id.invalid", "UserId must be specified."));
        if (guildId.Value == 0)
            return Validation<IgnoredMember>.Fail(
                new Error("xp-ignored-member.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<IgnoredMember>.Fail(
                new Error("xp-ignored-member.set-by.invalid", "ModeratorId must be specified."));
        return Validation<IgnoredMember>.Succeed(
            new IgnoredMember { UserId = userId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}

public sealed record WatchedMember : XpWatchedItem
{
    public UserId UserId { get => new(Id); init => Id = value.Value; }

    public static Validation<WatchedMember> Create(
        UserId userId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (userId.Value == 0)
            return Validation<WatchedMember>.Fail(
                new Error("xp-watched-member.user-id.invalid", "UserId must be specified."));
        if (guildId.Value == 0)
            return Validation<WatchedMember>.Fail(
                new Error("xp-watched-member.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<WatchedMember>.Fail(
                new Error("xp-watched-member.set-by.invalid", "ModeratorId must be specified."));
        return Validation<WatchedMember>.Succeed(
            new WatchedMember { UserId = userId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}

public sealed record IgnoredRole : XpIgnoredItem
{
    public RoleId RoleId { get => new(Id); init => Id = value.Value; }

    public static Validation<IgnoredRole> Create(
        RoleId roleId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (roleId.Value == 0)
            return Validation<IgnoredRole>.Fail(
                new Error("xp-ignored-role.role-id.invalid", "RoleId must be specified."));
        if (guildId.Value == 0)
            return Validation<IgnoredRole>.Fail(
                new Error("xp-ignored-role.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<IgnoredRole>.Fail(
                new Error("xp-ignored-role.set-by.invalid", "ModeratorId must be specified."));
        return Validation<IgnoredRole>.Succeed(
            new IgnoredRole { RoleId = roleId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}

public sealed record WatchedRole : XpWatchedItem
{
    public RoleId RoleId { get => new(Id); init => Id = value.Value; }

    public static Validation<WatchedRole> Create(
        RoleId roleId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
    {
        if (roleId.Value == 0)
            return Validation<WatchedRole>.Fail(
                new Error("xp-watched-role.role-id.invalid", "RoleId must be specified."));
        if (guildId.Value == 0)
            return Validation<WatchedRole>.Fail(
                new Error("xp-watched-role.guild-id.invalid", "GuildId must be specified."));
        if (setBy.Value == 0)
            return Validation<WatchedRole>.Fail(
                new Error("xp-watched-role.set-by.invalid", "ModeratorId must be specified."));
        return Validation<WatchedRole>.Succeed(
            new WatchedRole { RoleId = roleId, GuildId = guildId, SetBy = setBy, SetAt = setAt });
    }
}
