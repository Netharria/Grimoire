// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public readonly record struct MessageId(ulong Value)
{
    public static bool TryParse(string? value, out MessageId messageId) =>
        (messageId = ulong.TryParse(value, out var id) ? new MessageId(id) : default) != default;
}

public readonly record struct UserId(ulong Value)
{
    public static bool TryParse(string? value, out UserId userId) =>
        (userId = ulong.TryParse(value, out var id) ? new UserId(id) : default) != default;
}

public readonly record struct ModeratorId(ulong Value)
{
    public static bool TryParse(string? value, out ModeratorId moderatorId) =>
        (moderatorId = ulong.TryParse(value, out var id) ? new ModeratorId(id) : default) != default;
}

public readonly record struct GuildId(ulong Value)
{
    public static bool TryParse(string? value, out GuildId guildId) =>
        (guildId = ulong.TryParse(value, out var id) ? new GuildId(id) : default) != default;
}

public readonly record struct ChannelId(ulong Value)
{
    public static bool TryParse(string? value, out ChannelId channelId) =>
        (channelId = ulong.TryParse(value, out var id) ? new ChannelId(id) : default) != default;
}

public readonly record struct RoleId(ulong Value)
{
    public static bool TryParse(string? value, out RoleId roleId) =>
        (roleId = ulong.TryParse(value, out var id) ? new RoleId(id) : default) != default;
}
