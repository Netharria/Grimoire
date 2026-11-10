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

    /// <summary>The channel, member or role this is about. Ids are snowflakes, so they don't collide across kinds.</summary>
    internal ulong TargetId => this.Id;
    public required GuildId GuildId { get; init; }
    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public abstract record XpIgnoredItem : XpTrackedItem;

public abstract record XpWatchedItem : XpTrackedItem;

public sealed record IgnoredChannel : XpIgnoredItem
{
    public required ChannelId ChannelId { get => new(Id); init => Id = value.Value; }

    public static IgnoredChannel Create(
        ChannelId channelId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new IgnoredChannel { ChannelId = channelId, GuildId = guildId, SetBy = setBy, SetAt = setAt };
}

public sealed record WatchedChannel : XpWatchedItem
{
    public required ChannelId ChannelId { get => new(Id); init => Id = value.Value; }

    public static WatchedChannel Create(
        ChannelId channelId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new WatchedChannel { ChannelId = channelId, GuildId = guildId, SetBy = setBy, SetAt = setAt };
}

public sealed record IgnoredMember : XpIgnoredItem
{
    public required UserId UserId { get => new(Id); init => Id = value.Value; }

    public static IgnoredMember Create(
        UserId userId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new IgnoredMember { UserId = userId, GuildId = guildId, SetBy = setBy, SetAt = setAt };
}

public sealed record WatchedMember : XpWatchedItem
{
    public required UserId UserId { get => new(Id); init => Id = value.Value; }

    public static WatchedMember Create(
        UserId userId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new WatchedMember { UserId = userId, GuildId = guildId, SetBy = setBy, SetAt = setAt };
}

public sealed record IgnoredRole : XpIgnoredItem
{
    public required RoleId RoleId { get => new(Id); init => Id = value.Value; }

    public static IgnoredRole Create(
        RoleId roleId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new IgnoredRole { RoleId = roleId, GuildId = guildId, SetBy = setBy, SetAt = setAt };
}

public sealed record WatchedRole : XpWatchedItem
{
    public required RoleId RoleId { get => new(Id); init => Id = value.Value; }

    public static WatchedRole Create(
        RoleId roleId, GuildId guildId, ModeratorId setBy, DateTimeOffset setAt)
        => new WatchedRole { RoleId = roleId, GuildId = guildId, SetBy = setBy, SetAt = setAt };
}
