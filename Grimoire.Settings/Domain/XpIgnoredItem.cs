// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Settings.Domain;

public abstract record XpIgnoredItem
{
    public required ulong Id { get; init; }
    public required GuildId GuildId { get; init; }
    public required ModeratorId SetBy { get; init; }
    public required DateTimeOffset SetAt { get; init; }
    public required bool Enabled { get; init; }
}

public sealed record IgnoredChannel : XpIgnoredItem
{
    public ChannelId ChannelId
    {
        get => new (Id);
        init => Id = value.Value;
    }
}

public sealed record IgnoredMember : XpIgnoredItem
{
    public UserId UserId
    {
        get => new (Id);
        init => Id = value.Value;
    }
}

public sealed record IgnoredRole : XpIgnoredItem
{
    public RoleId RoleId
    {
        get => new (Id);
        init => Id = value.Value;
    }
}

public enum IgnoredType
{
    Channel,
    Member,
    Role
}
