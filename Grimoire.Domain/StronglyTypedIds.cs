// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public readonly record struct MessageId(ulong Value)
{
    public static MessageId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new MessageId(id) : null;
}

public readonly record struct UserId(ulong Value)
{
    public static UserId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new UserId(id) : null;
}

public readonly record struct ModeratorId(ulong Value)
{
    public static ModeratorId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new ModeratorId(id) : null;
    public static explicit operator ModeratorId(UserId id) => new(id.Value);
    public static explicit operator UserId(ModeratorId id) => new(id.Value);
}

public readonly record struct GuildId(ulong Value)
{
    public static GuildId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new GuildId(id) : null;
}

public readonly record struct ChannelId(ulong Value)
{
    public static ChannelId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new ChannelId(id) : null;
}

public readonly record struct RoleId(ulong Value)
{
    public static RoleId? TryParse(string? value)
        => ulong.TryParse(value, out var id) ? new RoleId(id) : null;
}

public readonly record struct SinId(long Value);

public readonly record struct AttachmentId(ulong Value);
