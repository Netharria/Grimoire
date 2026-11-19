// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

/// <summary>
///     A sin's pardon: who first pardoned it, and when. Neither changes: a second <c>/Pardon</c> adds a reason
///     (Q8, Q8b). One per sin.
/// </summary>
[UsedImplicitly]
public sealed record Pardon
{
    public required SinId SinId { get; init; }
    public required ModeratorId ModeratorId { get; init; }
    public required DateTimeOffset SetAt { get; init; }
    public Sin? Sin { get; init; }
    public ICollection<PardonReasonHistory> ReasonHistory { get; init; } = [];
}

/// <summary>One wording of a pardon's reason. A re-pardon adds one, and the latest is the reason shown (Q8).</summary>
[UsedImplicitly]
public sealed record PardonReasonHistory
{
    public required SinId SinId { get; init; }
    public required ModerationReason Reason { get; init; }
    public required ModeratorId ModeratorId { get; init; }

    /// <summary>Part of the key, so held to the microsecond, as Postgres stores it (see <see cref="Create" />).</summary>
    public required DateTimeOffset SetAt { get; init; }

    public Pardon? Pardon { get; init; }

    /// <summary>
    ///     A reason row, its time truncated to the microsecond: .NET keeps 100 ns ticks, which Postgres would round
    ///     away, and the time is part of the key.
    /// </summary>
    public static PardonReasonHistory Create(
        SinId sinId, ModerationReason reason, ModeratorId moderatorId, DateTimeOffset setAt)
        => new()
        {
            SinId = sinId,
            Reason = reason,
            ModeratorId = moderatorId,
            SetAt = setAt.AddTicks(-(setAt.UtcTicks % TimeSpan.TicksPerMicrosecond))
        };
}
