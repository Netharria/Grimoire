// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

/// <summary>
///     Where a mute, channel lock or thread lock is in its life (TD-1). Each state holds exactly the who/when it
///     needs, as the tables' check constraints require. Transitions only move forward: Pending → Active, and Pending or
///     Active → Expired or Removed.
/// </summary>
public abstract record ItemState
{
    private ItemState() { }

    /// <summary>In force: recorded, and either applied (Active) or waiting to be (Pending).</summary>
    public bool InForce => this is Pending or Active;

    public Fin<ItemState> Activate(DateTimeOffset at)
        => this is Pending ? new Active(at) : Refused("activated");

    public Fin<ItemState> Expire(DateTimeOffset at)
        => this.InForce ? new Expired(at) : Refused("expired");

    public Fin<ItemState> Remove(ModeratorId by, ModerationReason? reason, DateTimeOffset at)
        => this.InForce ? new Removed(at, by, reason) : Refused("removed");

    private Fin<ItemState> Refused(string transition)
        => new Invalid($"An item that is {this.Name} can't be {transition}.");

    /// <summary>The stored name, as in the <c>State</c> column.</summary>
    public string Name => this switch
    {
        Pending => nameof(Pending),
        Active => nameof(Active),
        Expired => nameof(Expired),
        Removed => nameof(Removed),
        _ => throw new InvariantViolationException()
    };

    /// <summary>
    ///     A time as Postgres stores it, to the microsecond. A lock's set time is part of its key, so the value the
    ///     code holds must equal the stored one: .NET keeps 100 ns ticks, which Postgres would round away.
    /// </summary>
    public static DateTimeOffset Stored(DateTimeOffset time)
        => time.AddTicks(-(time.UtcTicks % TimeSpan.TicksPerMicrosecond));

    /// <summary>Recorded and not applied yet. Nothing writes it until the processors (3.4, 3.5).</summary>
    public sealed record Pending : ItemState;

    public sealed record Active(DateTimeOffset At) : ItemState;

    /// <summary>It ran out. A Pending item can expire before it is applied (TD-4).</summary>
    public sealed record Expired(DateTimeOffset At) : ItemState;

    /// <summary>A moderator ended it, or a newer mute or lock of the same member or channel replaced it.</summary>
    public sealed record Removed(DateTimeOffset At, ModeratorId By, ModerationReason? Reason) : ItemState;
}
