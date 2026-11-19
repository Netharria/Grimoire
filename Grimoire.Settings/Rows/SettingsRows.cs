// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain;
using Grimoire.Settings.Domain.Documents;

namespace Grimoire.Settings.Rows;

/// <summary>
///     The stored rows of the documents and the state tables (spec 2.2, section 3.2). EF maps these, not the domain
///     records, because a state union isn't a column; Dapper reads the same rows from 3.1 and 3.4 on.
/// </summary>
internal sealed class GuildSettingRow
{
    public required GuildId GuildId { get; init; }
    public required SettingsDocument Document { get; init; }
    public required int Version { get; init; }

    /// <summary>The document as <c>jsonb</c>, in <see cref="SettingsJson" />'s encoding.</summary>
    public required string Value { get; init; }

    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
}

/// <summary>The columns every state table shares (TD-1).</summary>
internal abstract class ItemRow
{
    public required GuildId GuildId { get; init; }
    public required ModeratorId ModeratorId { get; init; }
    public required DateTimeOffset SetAt { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required string State { get; init; }
    public DateTimeOffset? ActivatedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public ModeratorId? RemovedBy { get; init; }
    public ModerationReason? RemoveReason { get; init; }

    public ItemState ToState()
        => this.State switch
        {
            nameof(ItemState.Pending) => new ItemState.Pending(),
            nameof(ItemState.Active) => new ItemState.Active(Required(this.ActivatedAt)),
            nameof(ItemState.Expired) => new ItemState.Expired(Required(this.EndedAt)),
            nameof(ItemState.Removed) => new ItemState.Removed(Required(this.EndedAt), Required(this.RemovedBy),
                this.RemoveReason),
            _ => throw new InvariantViolationException("A stored item has an unknown state.")
        };

    // The check constraints guarantee these for each state.
    private static T Required<T>(T? value) where T : struct
        => value ?? throw new InvariantViolationException("A stored item is missing a field its state requires.");
}

internal sealed class MuteRow : ItemRow
{
    public required UserId UserId { get; init; }
    public required SinId SinId { get; init; }
    public RoleId? MuteRoleId { get; init; }

    public Mute ToMute()
        => new(this.GuildId, this.UserId, this.SinId, this.MuteRoleId, this.ModeratorId, this.SetAt,
            this.EndTime, this.ToState());
}

internal sealed class ChannelLockRow : ItemRow
{
    public required ChannelId ChannelId { get; init; }
    public ModerationReason? Reason { get; init; }
    public required PreviouslyAllowedPermissions PreviouslyAllowed { get; init; }
    public required PreviouslyDeniedPermissions PreviouslyDenied { get; init; }

    public ChannelLock ToLock()
        => new(this.GuildId, this.ChannelId, this.Reason, this.ModeratorId, this.SetAt, this.EndTime,
            this.PreviouslyAllowed, this.PreviouslyDenied, this.ToState());
}

internal sealed class ThreadLockRow : ItemRow
{
    public required ChannelId ChannelId { get; init; }
    public ModerationReason? Reason { get; init; }

    public ThreadLock ToLock()
        => new(this.GuildId, this.ChannelId, this.Reason, this.ModeratorId, this.SetAt, this.EndTime,
            this.ToState());
}
