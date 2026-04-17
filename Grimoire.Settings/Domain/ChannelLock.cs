// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public abstract record ChannelLock
{
    public required ModeratorId ModeratorId { get; init; }
    public required string Reason { get; init; }
    public required ChannelId ChannelId { get; init; }
    public required GuildId GuildId { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

public sealed record ChannelLockEvent : ChannelLock
{
    public required PreviouslyAllowedPermissions PreviouslyAllowed { get; init; }
    public required PreviouslyDeniedPermissions PreviouslyDenied { get; init; }
    public required DateTimeOffset EndTime { get; init; }
}

public sealed record ChannelUnlockEvent : ChannelLock;

public readonly record struct PreviouslyAllowedPermissions(long Permissions);

public readonly record struct PreviouslyDeniedPermissions(long Permissions);
