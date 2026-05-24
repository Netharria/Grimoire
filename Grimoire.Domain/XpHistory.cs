// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

/// <summary>A non-zero positive XP amount. Used for Earned, Awarded, and Migrated entries.</summary>
public readonly record struct PositiveXpAmount
{
    private PositiveXpAmount(long value)
    {
        Value = value;
    }

    public long Value { get; }

    /// <summary>Bypasses validation for EF Core materialisation. DB data is trusted post-migration.</summary>
    internal static PositiveXpAmount FromDatabase(long value) => new(value);

    public static Validation<PositiveXpAmount> Create(long value)
        => value > 0
            ? Validation<PositiveXpAmount>.Succeed(new PositiveXpAmount(value))
            : Validation<PositiveXpAmount>.Fail(
                new Error("xp-amount.must-be-positive", "XP amount must be greater than zero."));

    public override string ToString() => Value.ToString();
}

/// <summary>A non-zero negative XP amount. Used for Reclaimed entries.</summary>
public readonly record struct NegativeXpAmount
{
    private NegativeXpAmount(long value)
    {
        Value = value;
    }

    public long Value { get; }

    /// <summary>Bypasses validation for EF Core materialisation. DB data is trusted post-migration.</summary>
    internal static NegativeXpAmount FromDatabase(long value) => new(value);

    public static Validation<NegativeXpAmount> Create(long value)
        => value < 0
            ? Validation<NegativeXpAmount>.Succeed(new NegativeXpAmount(value))
            : Validation<NegativeXpAmount>.Fail(
                new Error("xp-amount.must-be-negative", "XP amount must be less than zero."));

    public override string ToString() => Value.ToString();
}

[UsedImplicitly]
public abstract record XpHistoryEntry
{
    public required DateTimeOffset TimeOut { get; init; }
    public required UserId UserId { get; init; }
    public required GuildId GuildId { get; init; }

    /// <summary>
    ///     Raw XP value from the database column. Use the typed <c>Xp</c> property on the
    ///     concrete subtype when possible; use this for EF Core aggregate queries across all subtypes.
    /// </summary>
    internal long RawXp { get; init; }
}

/// <summary>XP earned organically by the user through activity.</summary>
[UsedImplicitly]
public sealed record EarnedXp : XpHistoryEntry
{
    public PositiveXpAmount Xp => PositiveXpAmount.FromDatabase(RawXp);

    public static Validation<EarnedXp> Create(
        PositiveXpAmount xp, UserId userId, GuildId guildId, DateTimeOffset timeOut)
        => Validation<EarnedXp>.Succeed(new EarnedXp
        {
            RawXp = xp.Value, UserId = userId, GuildId = guildId, TimeOut = timeOut
        });
}

/// <summary>XP awarded to the user by a moderator.</summary>
[UsedImplicitly]
public sealed record AwardedXp : XpHistoryEntry
{
    public PositiveXpAmount Xp => PositiveXpAmount.FromDatabase(RawXp);
    public required ModeratorId AwarderId { get; init; }

    public static Validation<AwardedXp> Create(
        PositiveXpAmount xp, ModeratorId awarderId, UserId userId, GuildId guildId, DateTimeOffset timeOut)
    {
        if (awarderId.Value == 0)
            return Validation<AwardedXp>.Fail(
                new Error("awarded-xp.awarder-id.invalid", "AwarderId must be specified."));
        return Validation<AwardedXp>.Succeed(new AwardedXp
        {
            RawXp = xp.Value,
            AwarderId = awarderId,
            UserId = userId,
            GuildId = guildId,
            TimeOut = timeOut
        });
    }
}

/// <summary>XP reclaimed from the user by a moderator. Xp value is negative.</summary>
[UsedImplicitly]
public sealed record ReclaimedXp : XpHistoryEntry
{
    public NegativeXpAmount Xp => NegativeXpAmount.FromDatabase(RawXp);

    public static Validation<ReclaimedXp> Create(
        NegativeXpAmount xp, UserId userId, GuildId guildId, DateTimeOffset timeOut)
        => Validation<ReclaimedXp>.Succeed(new ReclaimedXp
        {
            RawXp = xp.Value, UserId = userId, GuildId = guildId, TimeOut = timeOut
        });
}

/// <summary>
///     XP carried over from a legacy database migration. Read-only historical record;
///     no factory is provided because no new Migrated entries will be created.
///     EF Core materialises these directly from the database.
/// </summary>
[UsedImplicitly]
public sealed record MigratedXp : XpHistoryEntry
{
    public PositiveXpAmount Xp => PositiveXpAmount.FromDatabase(RawXp);
}
