// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public sealed record IgnoredMember : IIgnored
{
    public UserId UserId { get; init; }

    public ulong Id
    {
        get => UserId.Value;
        init => UserId = new UserId(value);
    }

    public GuildId GuildId { get; init; }
}
