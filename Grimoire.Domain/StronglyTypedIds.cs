// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public readonly record struct MessageId(ulong Value) : IComparable<MessageId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(MessageId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

public readonly record struct UserId(ulong Value) : IComparable<UserId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(UserId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

public readonly record struct ModeratorId(ulong Value) : IComparable<ModeratorId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(ModeratorId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

public readonly record struct GuildId(ulong Value) : IComparable<GuildId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(GuildId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

public readonly record struct ChannelId(ulong Value) : IComparable<ChannelId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(ChannelId other) => this.Value.CompareTo(other.Value);

    public static ChannelId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new ChannelId(id) : null;

    public override string ToString() => this.Value.ToString();
}

public readonly record struct RoleId(ulong Value) : IComparable<RoleId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(RoleId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

/// <summary>
///     Uses <see langword="long" /> (not <see langword="ulong" />) because the backing column is a PostgreSQL
///     <c>bigserial</c> (signed 64-bit integer), not a Discord snowflake. Changing to <see langword="ulong" />
///     would silently truncate any ID above <see cref="long.MaxValue" /> on round-trip through EF Core.
/// </summary>
public readonly record struct SinId(long Value) : IComparable<SinId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(SinId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

public readonly record struct AttachmentId(ulong Value) : IComparable<AttachmentId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(AttachmentId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}

public readonly record struct EmojiId(ulong Value) : IComparable<EmojiId>
{
    /// <summary>Orders by value, so ids can key LanguageExt's sorted <c>Map</c> and <c>Set</c> (spec 2.5).</summary>
    public int CompareTo(EmojiId other) => this.Value.CompareTo(other.Value);

    public override string ToString() => this.Value.ToString();
}
