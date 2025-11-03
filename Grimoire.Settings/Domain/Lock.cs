// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed class Lock
{
    public required PreviouslyAllowedPermissions PreviouslyAllowed { get; init; }
    public required PreviouslyDeniedPermissions PreviouslyDenied { get; init; }
    public required ModeratorId ModeratorId { get; set; }
    public required string Reason { get; init; }
    public DateTimeOffset EndTime { get; set; }
    public required ChannelId ChannelId { get; init; }
    public required GuildId GuildId { get; init; }
}

public readonly record struct PreviouslyAllowedPermissions(long Permissions);
public readonly record struct PreviouslyDeniedPermissions(long Permissions);
